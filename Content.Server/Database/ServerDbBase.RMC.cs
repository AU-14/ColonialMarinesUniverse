using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RMC14.LinkAccount;
using Content.Server.IP;
using Content.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<Guid?> GetLinkingCode(Guid player)
    {
        await using var db = await GetDb();
        var linking = await db.DbContext.RMCLinkingCodes.FirstOrDefaultAsync(l => l.PlayerId == player);
        return linking?.Code;
    }

    public async Task SetLinkingCode(Guid player, Guid code)
    {
        await using var db = await GetDb();
        var linking = await db.DbContext.RMCLinkingCodes.FirstOrDefaultAsync(l => l.PlayerId == player);
        if (linking == null)
        {
            linking = new RMCLinkingCodes { PlayerId = player };
            db.DbContext.RMCLinkingCodes.Add(linking);
        }

        linking.Code = code;
        linking.CreationTime = DateTime.UtcNow;
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<bool> HasLinkedAccount(Guid player, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.RMCLinkedAccounts.AnyAsync(l => l.PlayerId == player, cancel);
    }

    public async Task<RMCPatron?> GetPatron(Guid player, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.RMCPatrons
            .Include(p => p.Tier)
            .Include(p => p.LobbyMessage)
            .Include(p => p.RoundEndMarineShoutout)
            .Include(p => p.RoundEndXenoShoutout)
            .FirstOrDefaultAsync(p => p.PlayerId == player, cancellationToken: cancel);
    }

    public async Task<List<RMCPatron>> GetAllPatrons()
    {
        await using var db = await GetDb();
        return await db.DbContext.RMCPatrons
            .Include(p => p.Player)
            .Include(p => p.Tier)
            .ToListAsync();
    }

    public async Task SetGhostColor(Guid player, System.Drawing.Color? color)
    {
        await using var db = await GetDb();
        var patron = await db.DbContext.RMCPatrons.FirstOrDefaultAsync(p => p.PlayerId == player);
        if (patron == null)
            return;

        patron.GhostColor = color?.ToArgb();
        await db.DbContext.SaveChangesAsync();
    }

    public async Task SetLobbyMessage(Guid player, string message)
    {
        await using var db = await GetDb();
        var entry = await db.DbContext.RMCPatronLobbyMessages
            .FirstOrDefaultAsync(p => p.PatronId == player);
        entry ??= db.DbContext.RMCPatronLobbyMessages.Add(new RMCPatronLobbyMessage
        {
            PatronId = player,
        }).Entity;
        entry.Message = message;
        await db.DbContext.SaveChangesAsync();
    }

    public async Task SetMarineShoutout(Guid player, string name)
    {
        await using var db = await GetDb();
        var entry = await db.DbContext.RMCPatronRoundEndMarineShoutouts
            .FirstOrDefaultAsync(p => p.PatronId == player);
        entry ??= db.DbContext.RMCPatronRoundEndMarineShoutouts.Add(new RMCPatronRoundEndMarineShoutout
        {
            PatronId = player,
        }).Entity;
        entry.Name = name;
        await db.DbContext.SaveChangesAsync();
    }

    public async Task SetXenoShoutout(Guid player, string name)
    {
        await using var db = await GetDb();
        var entry = await db.DbContext.RMCPatronRoundEndXenoShoutouts
            .FirstOrDefaultAsync(p => p.PatronId == player);
        entry ??= db.DbContext.RMCPatronRoundEndXenoShoutouts.Add(new RMCPatronRoundEndXenoShoutout
        {
            PatronId = player,
        }).Entity;
        entry.Name = name;
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<(string Message, string User)?> GetRandomLobbyMessage()
    {
        await using var db = await GetDb();
        var messages = await db.DbContext.RMCPatronLobbyMessages
            .Include(p => p.Patron)
            .ThenInclude(p => p.Player)
            .Where(p => p.Patron.Tier.LobbyMessage)
            .Where(p => !string.IsNullOrWhiteSpace(p.Message))
            .Select(p => new { p.Message, p.Patron.Player.LastSeenUserName })
            .ToListAsync();

        if (messages.Count == 0)
            return null;

        var random = messages[Random.Shared.Next(messages.Count)];
        return (random.Message, random.LastSeenUserName);
    }

    public async Task<(RoundEndShoutout? Marine, RoundEndShoutout? Xeno)> GetRandomShoutout()
    {
        await using var db = await GetDb();
        var marines = await db.DbContext.RMCPatronRoundEndMarineShoutouts
            .Include(p => p.Patron)
            .ThenInclude(p => p.Player)
            .Where(p => p.Patron.Tier.RoundEndShoutout)
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToListAsync();
        var xenos = await db.DbContext.RMCPatronRoundEndXenoShoutouts
            .Include(p => p.Patron)
            .ThenInclude(p => p.Player)
            .Where(p => p.Patron.Tier.RoundEndShoutout)
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToListAsync();

        var marine = marines.Count == 0 ? null : marines[Random.Shared.Next(marines.Count)];
        var xeno = xenos.Count == 0 ? null : xenos[Random.Shared.Next(xenos.Count)];
        return (
            marine == null ? null : new RoundEndShoutout(marine.Patron.Player.LastSeenUserName, marine.Name),
            xeno == null ? null : new RoundEndShoutout(xeno.Patron.Player.LastSeenUserName, xeno.Name));
    }

    public async Task<List<string>> GetExcludedRoleTimers(Guid player, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.RMCRoleTimerExcludes
            .Where(r => r.PlayerId == player)
            .Select(r => r.Tracker)
            .ToListAsync(cancel);
    }

    public async Task<bool> ExcludeRoleTimer(Guid player, string tracker)
    {
        await using var db = await GetDb();
        if (await db.DbContext.RMCRoleTimerExcludes.AnyAsync(r => r.PlayerId == player && r.Tracker == tracker))
            return false;

        db.DbContext.RMCRoleTimerExcludes.Add(new RMCRoleTimerExclude
        {
            PlayerId = player,
            Tracker = tracker,
        });
        await db.DbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveRoleTimerExclusion(Guid player, string tracker)
    {
        await using var db = await GetDb();
        var exclusion = await db.DbContext.RMCRoleTimerExcludes
            .FirstOrDefaultAsync(r => r.PlayerId == player && r.Tracker == tracker);
        if (exclusion == null)
            return false;

        db.DbContext.RMCRoleTimerExcludes.Remove(exclusion);
        await db.DbContext.SaveChangesAsync();
        return true;
    }

    public async Task AddCommendation(Guid giver,
        Guid receiver,
        string giverName,
        string receiverName,
        string name,
        string text,
        CommendationType type,
        int round)
    {
        await using var db = await GetDb();
        db.DbContext.RMCCommendations.Add(new RMCCommendation
        {
            GiverId = giver,
            ReceiverId = receiver,
            GiverName = giverName,
            ReceiverName = receiverName,
            Name = name,
            Text = text,
            Type = type,
            RoundId = round,
        });
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<List<RMCCommendation>> GetCommendationsReceived(Guid player,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(db.DbContext.RMCCommendations.Where(c => !c.Deleted), includePlayers);
        if (filterType.HasValue)
            query = query.Where(c => c.Type == filterType.Value);
        return await query.Where(c => c.ReceiverId == player).ToListAsync();
    }

    public async Task<List<RMCCommendation>> GetCommendationsGiven(Guid player,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(db.DbContext.RMCCommendations.Where(c => !c.Deleted), includePlayers);
        if (filterType.HasValue)
            query = query.Where(c => c.Type == filterType.Value);
        return await query.Where(c => c.GiverId == player).ToListAsync();
    }

    public async Task<List<RMCCommendation>> GetLastCommendations(int count,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(db.DbContext.RMCCommendations.Where(c => !c.Deleted), includePlayers);
        if (filterType.HasValue)
            query = query.Where(c => c.Type == filterType.Value);
        return await query.OrderByDescending(c => c.Id).Take(count).ToListAsync();
    }

    public async Task<RMCCommendation?> GetCommendationById(int commendationId, bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(db.DbContext.RMCCommendations.Where(c => !c.Deleted), includePlayers);
        return await query.FirstOrDefaultAsync(c => c.Id == commendationId);
    }

    public async Task<List<RMCCommendation>> GetCommendationsByRound(int roundId,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(db.DbContext.RMCCommendations.Where(c => !c.Deleted), includePlayers);
        if (filterType.HasValue)
            query = query.Where(c => c.Type == filterType.Value);
        return await query.Where(c => c.RoundId == roundId).ToListAsync();
    }

    public async Task<RMCCommendation?> DeleteCommendationById(int commendationId,
        Guid deletedBy,
        DateTimeOffset deletedAt,
        bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(db.DbContext.RMCCommendations.Where(c => !c.Deleted), includePlayers);
        var commendation = await query.FirstOrDefaultAsync(c => c.Id == commendationId);
        if (commendation == null)
            return null;

        commendation.Deleted = true;
        commendation.DeletedById = deletedBy;
        commendation.DeletedAt = deletedAt.UtcDateTime;
        await db.DbContext.SaveChangesAsync();
        return commendation;
    }

    public async Task<List<RMCCommendation>> DeleteCommendationsByRound(int roundId,
        CommendationType type,
        Guid deletedBy,
        DateTimeOffset deletedAt,
        Guid? giverId = null,
        Guid? receiverId = null,
        bool includePlayers = false)
    {
        await using var db = await GetDb();
        var query = IncludeCommendationPlayers(
            db.DbContext.RMCCommendations.Where(c => !c.Deleted && c.RoundId == roundId && c.Type == type),
            includePlayers);
        if (giverId.HasValue)
            query = query.Where(c => c.GiverId == giverId.Value);
        if (receiverId.HasValue)
            query = query.Where(c => c.ReceiverId == receiverId.Value);

        var commendations = await query.ToListAsync();
        foreach (var commendation in commendations)
        {
            commendation.Deleted = true;
            commendation.DeletedById = deletedBy;
            commendation.DeletedAt = deletedAt.UtcDateTime;
        }

        await db.DbContext.SaveChangesAsync();
        return commendations;
    }

    private static IQueryable<RMCCommendation> IncludeCommendationPlayers(
        IQueryable<RMCCommendation> query,
        bool includePlayers)
    {
        return includePlayers
            ? query.Include(c => c.Giver).Include(c => c.Receiver)
            : query;
    }

    public async Task IncreaseInfects(Guid player)
    {
        await using var db = await GetDb();
        var stats = await db.DbContext.RMCPlayerStats.FirstOrDefaultAsync(s => s.PlayerId == player);
        stats ??= db.DbContext.RMCPlayerStats.Add(new RMCPlayerStats { PlayerId = player }).Entity;
        stats.ParasiteInfects++;
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>?> GetActionOrder(Guid player)
    {
        await using var db = await GetDb();
        return await db.DbContext.RMCPlayerActionOrder
            .Where(a => a.PlayerId == player)
            .ToDictionaryAsync(a => a.Id, a => a.Actions);
    }

    public async Task SetActionOrder(Guid player, string id, List<string> actions)
    {
        await using var db = await GetDb();
        var order = await db.DbContext.RMCPlayerActionOrder
            .FirstOrDefaultAsync(a => a.PlayerId == player && a.Id == id);
        order ??= db.DbContext.RMCPlayerActionOrder.Add(new RMCPlayerActionOrder
        {
            PlayerId = player,
            Id = id,
        }).Entity;
        order.Actions = new List<string>(actions);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<HashSet<string>> GetLarvaPoolOptOuts(Guid player)
    {
        await using var db = await GetDb();
        return await db.DbContext.RMCLarvaPoolOptOuts
            .Where(o => o.PlayerId == player)
            .Select(o => o.HiveId)
            .ToHashSetAsync();
    }

    public async Task SetLarvaPoolOptIn(Guid player, string hiveId, bool optedIn)
    {
        await using var db = await GetDb();
        var optOut = await db.DbContext.RMCLarvaPoolOptOuts
            .FirstOrDefaultAsync(o => o.PlayerId == player && o.HiveId == hiveId);

        if (optedIn)
        {
            if (optOut != null)
                db.DbContext.RMCLarvaPoolOptOuts.Remove(optOut);
        }
        else if (optOut == null)
        {
            db.DbContext.RMCLarvaPoolOptOuts.Add(new RMCLarvaPoolOptOut
            {
                PlayerId = player,
                HiveId = hiveId,
            });
        }

        await db.DbContext.SaveChangesAsync();
    }

    public async Task AddChatBan(int? round,
        NetUserId target,
        (IPAddress, int)? addressRange,
        ImmutableTypedHwid? hwid,
        TimeSpan? duration,
        ChatType type,
        NetUserId admin,
        string reason)
    {
        await using var db = await GetDb();
        var time = DateTime.UtcNow;
        db.DbContext.RMCPlayerChatBans.Add(new RMCChatBans
        {
            RoundId = round,
            PlayerId = target,
            Address = addressRange is { } range ? range.ToNpgsqlInet() : default,
            HWId = hwid,
            Type = type,
            BanningAdminId = admin,
            Reason = reason,
            BannedAt = time,
            ExpiresAt = duration == null ? null : time.Add(duration.Value),
        });
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<List<RMCChatBans>> GetAllChatBans(Guid player)
    {
        await using var db = await GetDb();
        return await db.DbContext.RMCPlayerChatBans
            .Include(b => b.UnbanningAdmin)
            .Where(c => c.PlayerId == player)
            .ToListAsync();
    }

    public async Task<List<RMCChatBans>> GetActiveChatBans(Guid player)
    {
        await using var db = await GetDb();
        return await db.DbContext.RMCPlayerChatBans
            .Include(b => b.UnbanningAdmin)
            .Where(c => c.PlayerId == player)
            .Where(c => c.UnbannedAt == null && (c.ExpiresAt == null || c.ExpiresAt.Value > DateTime.UtcNow))
            .ToListAsync();
    }

    public async Task<Guid?> TryPardonChatBan(int id, Guid? admin)
    {
        await using var db = await GetDb();
        var ban = await db.DbContext.RMCPlayerChatBans.FirstOrDefaultAsync(c => c.Id == id);
        if (ban == null || ban.UnbanningAdminId != null)
            return null;

        ban.UnbanningAdminId = admin;
        ban.UnbannedAt = DateTime.UtcNow;
        await db.DbContext.SaveChangesAsync();
        return ban.PlayerId;
    }
}
