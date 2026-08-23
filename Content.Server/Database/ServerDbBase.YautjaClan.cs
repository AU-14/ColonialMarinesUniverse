using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared._CMU14.Yautja;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<YautjaClanRecord?> GetYautjaClanAsync(int clanId)
    {
        await using var db = await GetDb();
        var clan = await db.DbContext.YautjaClans.SingleOrDefaultAsync(entry => entry.Id == clanId);
        return clan == null ? null : ToRecord(clan);
    }

    public async Task<List<YautjaClanRecord>> GetYautjaClansAsync()
    {
        await using var db = await GetDb();
        var clans = await db.DbContext.YautjaClans
            .Where(entry => entry.Active)
            .OrderBy(entry => entry.Name)
            .ToListAsync();
        return clans.Select(ToRecord).ToList();
    }

    public async Task<YautjaClanMemberRecord?> GetYautjaClanMemberAsync(Guid userId)
    {
        await using var db = await GetDb();
        var member = await db.DbContext.YautjaClanMembers
            .SingleOrDefaultAsync(entry => entry.PlayerUserId == userId);
        return member == null ? null : ToRecord(member);
    }

    public async Task<List<YautjaClanMemberRecord>> GetYautjaClanMembersAsync(int? clanId = null)
    {
        await using var db = await GetDb();
        var query = db.DbContext.YautjaClanMembers.AsQueryable();
        if (clanId is { } id)
            query = query.Where(entry => entry.ClanId == id);

        var members = await query.OrderByDescending(entry => entry.Rank).ToListAsync();
        return members.Select(ToRecord).ToList();
    }

    public async Task<List<YautjaClanMemberRecord>> GetYautjaClanlessMembersAsync()
    {
        await using var db = await GetDb();
        var members = await db.DbContext.YautjaClanMembers
            .Where(entry => entry.ClanId == null)
            .OrderByDescending(entry => entry.Rank)
            .ToListAsync();
        return members.Select(ToRecord).ToList();
    }

    public async Task<List<YautjaWhitelistHolderRecord>> GetYautjaWhitelistHoldersAsync()
    {
        await using var db = await GetDb();
        var players = await db.DbContext.Player
            .Where(entry => entry.YautjaWhitelistFlags != (int) YautjaWhitelistFlags.None)
            .OrderBy(entry => entry.LastSeenUserName)
            .Select(entry => new
            {
                entry.UserId,
                entry.LastSeenUserName,
                entry.YautjaRank,
                entry.YautjaWhitelistFlags,
            })
            .ToListAsync();
        return players
            .Select(entry => new YautjaWhitelistHolderRecord(
                entry.UserId,
                entry.LastSeenUserName,
                entry.YautjaRank,
                entry.YautjaWhitelistFlags))
            .ToList();
    }

    public async Task<int> CreateYautjaClanAsync(
        string name,
        string description,
        int honor,
        string color,
        bool active = true)
    {
        await using var db = await GetDb();
        var clan = new YautjaClan
        {
            Name = name,
            Description = description,
            Honor = honor,
            Color = color,
            Active = active,
        };
        db.DbContext.YautjaClans.Add(clan);
        await db.DbContext.SaveChangesAsync();
        return clan.Id;
    }

    public async Task<bool> UpdateYautjaClanAsync(
        int clanId,
        string name,
        string description,
        string color)
    {
        await using var db = await GetDb();
        var updated = await db.DbContext.YautjaClans
            .Where(entry => entry.Id == clanId && entry.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.Name, name)
                .SetProperty(entry => entry.Description, description)
                .SetProperty(entry => entry.Color, color));
        return updated == 1;
    }

    public async Task<bool> UpdateYautjaClanHonorAsync(int clanId, int honor)
    {
        await using var db = await GetDb();
        var updated = await db.DbContext.YautjaClans
            .Where(entry => entry.Id == clanId && entry.Active)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Honor, honor));
        return updated == 1;
    }

    public async Task<YautjaClanDeleteResult> DeactivateYautjaClanAsync(int clanId)
    {
        await using var db = await GetDb();
        await using var transaction = await db.DbContext.Database.BeginTransactionAsync();

        var deactivated = await db.DbContext.YautjaClans
            .Where(entry => entry.Id == clanId && entry.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.Active, false));
        if (deactivated != 1)
            return new(false, []);

        var members = await db.DbContext.YautjaClanMembers
            .Where(entry => entry.ClanId == clanId)
            .ToListAsync();
        var detachedPlayers = members
            .Select(entry => entry.PlayerUserId)
            .ToList();

        foreach (var member in members)
        {
            var preserveAncient = member.Rank == (int) YautjaRank.Ancient;
            member.ClanId = null;
            if (!preserveAncient)
            {
                member.Rank = (int) YautjaRank.Blooded;
                member.Permissions = (int) YautjaClanPermission.UserAll;
                member.IsLegacy = false;
            }

            await db.DbContext.Player
                .Where(entry => entry.UserId == member.PlayerUserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    entry => entry.YautjaRank,
                    (int?) member.Rank));
        }

        await db.DbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true, detachedPlayers);
    }

    public async Task<bool> UpsertYautjaClanMemberAsync(YautjaClanMemberRecord member)
    {
        await using var db = await GetDb();
        await using var transaction = await db.DbContext.Database.BeginTransactionAsync();

        if (member.ClanId is { } clanId)
        {
            var activeClanLocked = await db.DbContext.YautjaClans
                .Where(entry => entry.Id == clanId && entry.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(entry => entry.Active, entry => entry.Active));
            if (activeClanLocked != 1)
                return false;
        }

        var player = await db.DbContext.Player
            .SingleOrDefaultAsync(entry => entry.UserId == member.PlayerUserId);
        if (player == null)
            throw new InvalidOperationException($"Cannot set Yautja clan member for unknown player {member.PlayerUserId}.");

        var existing = await db.DbContext.YautjaClanMembers
            .SingleOrDefaultAsync(entry => entry.PlayerUserId == member.PlayerUserId);
        if (existing == null)
        {
            db.DbContext.YautjaClanMembers.Add(new YautjaClanMember
            {
                PlayerUserId = member.PlayerUserId,
                ClanId = member.ClanId,
                Rank = member.Rank,
                Permissions = member.Permissions,
                Honor = member.Honor,
                IsLegacy = member.IsLegacy,
            });
        }
        else
        {
            existing.ClanId = member.ClanId;
            existing.Rank = member.Rank;
            existing.Permissions = member.Permissions;
            existing.Honor = member.Honor;
            existing.IsLegacy = member.IsLegacy;
        }

        player.YautjaRank = member.Rank;
        await db.DbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteYautjaClanMemberAsync(Guid userId)
    {
        await using var db = await GetDb();
        await using var transaction = await db.DbContext.Database.BeginTransactionAsync();

        var deleted = await db.DbContext.YautjaClanMembers
            .Where(entry => entry.PlayerUserId == userId)
            .ExecuteDeleteAsync();
        if (deleted != 1)
            return false;

        var updated = await db.DbContext.Player
            .Where(entry => entry.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.YautjaRank, (int?) null));
        if (updated != 1)
            return false;

        await transaction.CommitAsync();
        return true;
    }

    public async Task<int> GetYautjaWhitelistFlagsAsync(Guid userId)
    {
        await using var db = await GetDb();
        return await db.DbContext.Player
            .Where(entry => entry.UserId == userId)
            .Select(entry => entry.YautjaWhitelistFlags)
            .SingleOrDefaultAsync();
    }

    public async Task SetYautjaWhitelistFlagsAsync(Guid userId, int flags)
    {
        await using var db = await GetDb();
        var player = await db.DbContext.Player
            .SingleOrDefaultAsync(entry => entry.UserId == userId);
        if (player == null)
            throw new InvalidOperationException($"Cannot set Yautja whitelist flags for unknown player {userId}.");

        player.YautjaWhitelistFlags = flags;

        if (flags == (int) YautjaWhitelistFlags.None)
        {
            // Removing the whitelist revokes the complete Yautja profile:
            // membership, membership rank, and the legacy compatibility rank
            // projection must not survive a whitelist removal.
            var member = await db.DbContext.YautjaClanMembers
                .SingleOrDefaultAsync(entry => entry.PlayerUserId == userId);
            if (member != null)
                db.DbContext.YautjaClanMembers.Remove(member);

            player.YautjaRank = null;
        }

        await db.DbContext.SaveChangesAsync();
    }

    private static YautjaClanRecord ToRecord(YautjaClan clan)
    {
        return new(clan.Id, clan.Name, clan.Description, clan.Honor, clan.Color, clan.Active);
    }

    private static YautjaClanMemberRecord ToRecord(YautjaClanMember member)
    {
        return new(member.PlayerUserId, member.ClanId, member.Rank, member.Permissions, member.Honor, member.IsLegacy);
    }
}
