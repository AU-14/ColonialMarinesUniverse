using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.Server.CMU14.Yautja;

// clan hall mutations. every one re-resolves both sides from the db and re-checks
// YautjaClanHallPolicy, the window's buttons are only a hint
public sealed partial class YautjaClanManager
{
    // any successful change, so every open hall can refresh
    public event Action? Changed;

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public async Task<YautjaClanHallActor> ResolveActor(NetUserId userId, bool staff, bool host)
    {
        return new(ToSnapshot(userId, await Resolve(userId)), staff, host);
    }

    public async Task<YautjaClanMemberSnapshot> ResolveSnapshot(NetUserId userId)
    {
        return ToSnapshot(userId, await Resolve(userId));
    }

    // same answer as Resolve, from a board row, so building the hall is one query instead of one per hunter
    public static YautjaClanMemberSnapshot FromBoard(YautjaHunterBoardRecord row)
    {
        var id = new NetUserId(row.PlayerUserId);
        var flags = (YautjaWhitelistFlags) row.WhitelistFlags;
        if (flags.HasFlag(YautjaWhitelistFlags.Leader) ||
            flags.HasFlag(YautjaWhitelistFlags.Council) ||
            flags.HasFlag(YautjaWhitelistFlags.CouncilLegacy))
        {
            return ToSnapshot(id, ResolveSpecial(flags, false, row.ClanId, row.Honor, row.IsLegacy));
        }

        var rank = SanitizeStoredRank(row.Rank);
        var permissions = !row.IsLegacy && TryReadPermissions(row.Permissions, out var stored)
            ? stored
            : PermissionsForRank(rank);
        if (rank == YautjaRank.Leader)
            permissions = PermissionsForRank(YautjaRank.Leader);

        return new(id, row.ClanId, rank, permissions, row.IsLegacy, row.Honor);
    }

    private static YautjaClanMutationResult Done(params NetUserId[] affected)
    {
        return new(true, null, affected);
    }

    private YautjaClanMutationResult Finish(YautjaClanMutationResult result)
    {
        if (result.Succeeded)
        {
            InvalidateCache(result.AffectedPlayers?.ToArray() ?? []);
            NotifyChanged();
        }

        return result;
    }

    public async Task<YautjaClanMutationResult> HallSetRank(YautjaClanHallActor actor, NetUserId targetId, YautjaRank rank)
    {
        var target = await ResolveSnapshot(targetId);
        if (!YautjaClanHallPolicy.CanSetRank(actor, target, rank))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-rank");

        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
                targetId.UserId,
                target.ClanId,
                (int) rank,
                (int) PermissionsForRank(rank),
                target.Honor,
                false)))
        {
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
        }

        return Finish(Done(targetId));
    }

    // ranks are clan positions, so leaving a clan drops you back to Blooded. Unblooded stay unblooded
    private static YautjaRank RankOutsideClan(YautjaClanMemberSnapshot target)
    {
        return target.Rank == YautjaRank.Unblooded ? YautjaRank.Unblooded : YautjaRank.Blooded;
    }

    public async Task<YautjaClanMutationResult> HallExile(YautjaClanHallActor actor, NetUserId targetId)
    {
        var target = await ResolveSnapshot(targetId);
        if (!YautjaClanHallPolicy.CanExile(actor, target))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-exile");

        return Finish(await PlaceMember(target, null));
    }

    public async Task<YautjaClanMutationResult> HallMove(YautjaClanHallActor actor, NetUserId targetId, int? clanId)
    {
        var target = await ResolveSnapshot(targetId);
        if (!YautjaClanHallPolicy.CanMove(actor, target) || target.ClanId == clanId)
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-move");

        return Finish(await PlaceMember(target, clanId));
    }

    private async Task<YautjaClanMutationResult> PlaceMember(YautjaClanMemberSnapshot target, int? clanId)
    {
        var keepAncient = target.Rank == YautjaRank.Ancient ||
                          YautjaClanPolicy.HasPermission(target.Permissions, YautjaClanPermission.AdminAncient);
        var rank = keepAncient ? YautjaRank.Ancient : RankOutsideClan(target);
        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
                target.PlayerId.UserId,
                clanId,
                (int) rank,
                (int) PermissionsForRank(rank),
                target.Honor,
                false)))
        {
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
        }

        return Done(target.PlayerId);
    }

    public async Task<YautjaClanMutationResult> HallSetAncient(YautjaClanHallActor actor, NetUserId targetId, bool enabled)
    {
        var target = await ResolveSnapshot(targetId);
        if (!YautjaClanHallPolicy.CanSetAncient(actor, target))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-ancient");

        var rank = enabled ? YautjaRank.Ancient : YautjaRank.Blooded;
        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
                targetId.UserId,
                target.ClanId,
                (int) rank,
                (int) PermissionsForRank(rank),
                target.Honor,
                false)))
        {
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
        }

        return Finish(Done(targetId));
    }

    public async Task<YautjaClanMutationResult> HallPurge(YautjaClanHallActor actor, NetUserId targetId)
    {
        var target = await ResolveSnapshot(targetId);
        if (!YautjaClanHallPolicy.CanPurge(actor, target))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-purge");

        if (!await _db.DeleteYautjaClanMemberAsync(targetId.UserId))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-no-profile");

        await _db.SetYautjaClanPetitionAsync(targetId.UserId, null);
        return Finish(Done(targetId));
    }

    public async Task<YautjaClanMutationResult> HallEditText(
        YautjaClanHallActor actor,
        int clanId,
        YautjaClanHallText field,
        string text)
    {
        var allowed = field switch
        {
            YautjaClanHallText.Description => YautjaClanHallPolicy.CanEditPresentation(actor, clanId),
            YautjaClanHallText.Notice => YautjaClanHallPolicy.CanEditNotice(actor, clanId),
            YautjaClanHallText.EliteNotes => YautjaClanHallPolicy.CanEditEliteNotes(actor, clanId),
            _ => false,
        };
        if (!allowed)
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-edit");

        text = text.Trim();
        if (text.Length > YautjaClanHallPolicy.MaxTextLength ||
            field == YautjaClanHallText.Description && text.Length == 0)
        {
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-text");
        }

        var clan = await _db.GetYautjaClanAsync(clanId);
        if (clan is not { Active: true })
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");

        var ok = field == YautjaClanHallText.Description
            ? await _db.UpdateYautjaClanAsync(clanId, clan.Name, text, clan.Color)
            : await _db.UpdateYautjaClanExtrasAsync(
                clanId,
                clan.Sigil,
                field == YautjaClanHallText.Notice ? text : clan.Notice,
                field == YautjaClanHallText.EliteNotes ? text : clan.EliteNotes);

        return ok
            ? Finish(Done())
            : YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
    }

    public async Task<YautjaClanMutationResult> HallEditLook(
        YautjaClanHallActor actor,
        int clanId,
        string name,
        string color,
        string sigil)
    {
        if (!YautjaClanHallPolicy.CanEditPresentation(actor, clanId))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-edit");

        var clan = await _db.GetYautjaClanAsync(clanId);
        if (clan is not { Active: true })
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");

        // only the council renames, a Leader's name field is ignored
        if (!YautjaClanHallPolicy.CanRename(actor))
            name = clan.Name;

        if (!YautjaClanSigils.IsValid(sigil))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-sigil");

        if (name.Trim().Length > YautjaClanHallPolicy.MaxNameLength)
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-name");

        if (!YautjaClanAdminValidation.TryNormalize(name, clan.Description, color, out var fields, out var error))
        {
            return YautjaClanMutationResult.Denied(error == YautjaClanAdminValidationError.InvalidColor
                ? "cmu-yautja-hall-denied-color"
                : "cmu-yautja-hall-denied-name");
        }

        if (!await _db.UpdateYautjaClanAsync(clanId, fields.Name, clan.Description, fields.Color) ||
            !await _db.UpdateYautjaClanExtrasAsync(clanId, sigil, clan.Notice, clan.EliteNotes))
        {
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
        }

        return Finish(Done());
    }

    public async Task<YautjaClanMutationResult> HallSetHonor(YautjaClanHallActor actor, int clanId, int honor)
    {
        if (!YautjaClanHallPolicy.CanSetHonor(actor))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-honor");

        if (honor < 0)
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-honor-value");

        return await _db.UpdateYautjaClanHonorAsync(clanId, honor)
            ? Finish(Done())
            : YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
    }

    public async Task<(YautjaClanMutationResult Result, int ClanId)> HallFoundClan(
        YautjaClanHallActor actor,
        string name,
        string color)
    {
        if (!YautjaClanHallPolicy.CanFoundOrDisband(actor))
            return (YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-found"), 0);

        // the description starts as a placeholder the leader is meant to overwrite
        var description = Loc.GetString("cmu-yautja-hall-new-clan-description");
        if (name.Trim().Length > YautjaClanHallPolicy.MaxNameLength)
            return (YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-name"), 0);

        if (!YautjaClanAdminValidation.TryNormalize(name, description, color, out var fields, out var error))
        {
            return (YautjaClanMutationResult.Denied(error == YautjaClanAdminValidationError.InvalidColor
                ? "cmu-yautja-hall-denied-color"
                : "cmu-yautja-hall-denied-name"), 0);
        }

        var id = await _db.CreateYautjaClanAsync(fields.Name, fields.Description, 0, fields.Color);
        await _db.UpdateYautjaClanExtrasAsync(id, YautjaClanSigils.Default, string.Empty, string.Empty);
        return (Finish(Done()), id);
    }

    public async Task<YautjaClanMutationResult> HallDisband(YautjaClanHallActor actor, int clanId)
    {
        if (!YautjaClanHallPolicy.CanFoundOrDisband(actor))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-found");

        var result = await _db.DeactivateYautjaClanAsync(clanId);
        if (!result.Succeeded)
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");

        return Finish(Done(result.DetachedPlayers.Select(player => new NetUserId(player)).ToArray()));
    }

    public async Task<YautjaClanMutationResult> HallPetition(YautjaClanHallActor actor, int? clanId)
    {
        if (clanId != null && !YautjaClanHallPolicy.CanPetition(actor))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-petition");

        return await _db.SetYautjaClanPetitionAsync(actor.Member.PlayerId.UserId, clanId)
            ? Finish(Done())
            : YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-clan-gone");
    }

    public async Task<YautjaClanMutationResult> HallAnswerPetition(
        YautjaClanHallActor actor,
        NetUserId targetId,
        bool accept)
    {
        var petition = (await _db.GetYautjaClanPetitionsAsync())
            .FirstOrDefault(entry => entry.PlayerUserId == targetId.UserId);
        if (petition == null)
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-no-petition");

        if (!YautjaClanHallPolicy.CanAnswerPetitions(actor, petition.ClanId))
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-petition-answer");

        if (!accept)
        {
            await _db.SetYautjaClanPetitionAsync(targetId.UserId, null);
            return Finish(Done());
        }

        var target = await ResolveSnapshot(targetId);
        if (target.ClanId != null)
        {
            // joined somewhere else in the meantime, the petition is stale
            await _db.SetYautjaClanPetitionAsync(targetId.UserId, null);
            NotifyChanged();
            return YautjaClanMutationResult.Denied("cmu-yautja-hall-denied-already-clanned");
        }

        // the upsert clears the petition on its way in
        var placed = await PlaceMember(target, petition.ClanId);
        return placed.Succeeded ? Finish(placed) : placed;
    }
}
