using System.Collections.Generic;
using System.Linq;

namespace Content.Shared.CMU14.Yautja;

// who is looking at the hall. staff = Clans admin flag, host = Host admin flag
public readonly record struct YautjaClanHallActor(YautjaClanMemberSnapshot Member, bool Staff, bool Host)
{
    public int? ClanId => Member.ClanId;
    public YautjaRank Rank => Member.Rank;
}

// the lore ladder. every rank adds a power, a hunter only ever acts on hunters below them,
// staff can do everything. the window hides what this says no to, the server re-checks all of it
public static class YautjaClanHallPolicy
{
    public const int MaxNameLength = 48;
    public const int MaxTextLength = 1500;

    // council = Ancient rank or the council permission set, works on any clan
    public static bool IsCouncil(YautjaClanHallActor actor)
    {
        return actor.Staff ||
               actor.Rank == YautjaRank.Ancient ||
               YautjaClanPolicy.HasPermission(actor.Member.Permissions, YautjaClanPermission.AdminAncient);
    }

    // the Leader whitelist flag, the one in-lore role that can raise or drop Ancients
    public static bool IsManager(YautjaClanHallActor actor)
    {
        return actor.Staff ||
               YautjaClanPolicy.HasPermission(actor.Member.Permissions, YautjaClanPermission.AdminManager);
    }

    private static bool IsAncientTier(YautjaClanMemberSnapshot target)
    {
        return target.Rank == YautjaRank.Ancient ||
               YautjaClanPolicy.HasPermission(target.Permissions, YautjaClanPermission.AdminAncient);
    }

    private static bool IsManagerTier(YautjaClanMemberSnapshot target)
    {
        return YautjaClanPolicy.HasPermission(target.Permissions, YautjaClanPermission.AdminManager);
    }

    private static bool InClan(YautjaClanHallActor actor, int? clanId)
    {
        return clanId != null && actor.ClanId == clanId;
    }

    public static bool CanViewClanPage(YautjaClanHallActor actor, int clanId)
    {
        return IsCouncil(actor) || actor.ClanId == clanId || actor.Rank >= YautjaRank.Blooded;
    }

    public static bool CanSeeEliteNotes(YautjaClanHallActor actor, int clanId)
    {
        return IsCouncil(actor) || InClan(actor, clanId) && actor.Rank >= YautjaRank.Elite;
    }

    public static bool CanEditNotice(YautjaClanHallActor actor, int clanId)
    {
        return IsCouncil(actor) || InClan(actor, clanId) && actor.Rank >= YautjaRank.Elder;
    }

    public static bool CanEditEliteNotes(YautjaClanHallActor actor, int clanId)
    {
        return CanEditNotice(actor, clanId);
    }

    // description, colour and sigil
    public static bool CanEditPresentation(YautjaClanHallActor actor, int clanId)
    {
        return IsCouncil(actor) || InClan(actor, clanId) && actor.Rank >= YautjaRank.Leader;
    }

    public static bool CanRename(YautjaClanHallActor actor)
    {
        return IsCouncil(actor);
    }

    public static bool CanSetHonor(YautjaClanHallActor actor)
    {
        return IsCouncil(actor);
    }

    public static bool CanFoundOrDisband(YautjaClanHallActor actor)
    {
        return IsCouncil(actor);
    }

    public static bool CanAnswerPetitions(YautjaClanHallActor actor, int clanId)
    {
        return IsCouncil(actor) || InClan(actor, clanId) && actor.Rank >= YautjaRank.Elder;
    }

    public static bool CanPetition(YautjaClanHallActor actor)
    {
        return actor.ClanId == null && actor.Rank != YautjaRank.YoungBlood;
    }

    public static bool CanManageWhitelist(YautjaClanHallActor actor)
    {
        return actor.Staff;
    }

    public static bool CanEditRound(YautjaClanHallActor actor)
    {
        return actor.Host;
    }

    // ancients are only touchable by staff or the council manager, and managers only by staff
    public static bool Outranks(YautjaClanHallActor actor, YautjaClanMemberSnapshot target)
    {
        if (actor.Member.PlayerId == target.PlayerId)
            return false;

        if (actor.Staff)
            return true;

        if (IsManagerTier(target))
            return false;

        if (IsAncientTier(target))
            return IsManager(actor);

        return actor.Rank > target.Rank || IsCouncil(actor);
    }

    // highest rank this actor hands out inside the given clan, null if none
    public static YautjaRank? MaxAssignableRank(YautjaClanHallActor actor, int? clanId)
    {
        if (clanId == null)
            return null;

        if (IsCouncil(actor))
            return YautjaRank.Leader;

        if (!InClan(actor, clanId))
            return null;

        return actor.Rank switch
        {
            YautjaRank.Leader => YautjaRank.Elder,
            YautjaRank.Elder => YautjaRank.Blooded,
            _ => null,
        };
    }

    // an Elder handles Unblooded and Blooded, a Leader everything up to Elder. the target has
    // to sit inside that band too, so an Elder can't knock an Elite down
    public static bool CanSetRank(YautjaClanHallActor actor, YautjaClanMemberSnapshot target, YautjaRank requested)
    {
        if (target.ClanId == null ||
            target.Rank == requested ||
            !YautjaClanPolicy.GetNormalAssignableRanks().Contains(requested) ||
            !Outranks(actor, target) ||
            IsAncientTier(target))
        {
            return false;
        }

        if (MaxAssignableRank(actor, target.ClanId) is not { } max)
            return false;

        return requested <= max && target.Rank <= max;
    }

    public static IReadOnlyList<YautjaRank> AssignableRanks(YautjaClanHallActor actor, YautjaClanMemberSnapshot target)
    {
        return YautjaClanPolicy.GetNormalAssignableRanks()
            .Where(rank => CanSetRank(actor, target, rank))
            .ToList();
    }

    public static bool CanExile(YautjaClanHallActor actor, YautjaClanMemberSnapshot target)
    {
        if (target.ClanId == null || !Outranks(actor, target))
            return false;

        return IsCouncil(actor) || InClan(actor, target.ClanId) && actor.Rank >= YautjaRank.Leader;
    }

    public static bool CanMove(YautjaClanHallActor actor, YautjaClanMemberSnapshot target)
    {
        return IsCouncil(actor) && Outranks(actor, target);
    }

    public static bool CanSetAncient(YautjaClanHallActor actor, YautjaClanMemberSnapshot target)
    {
        return IsManager(actor) && !IsManagerTier(target) && actor.Member.PlayerId != target.PlayerId;
    }

    // wipes the whole yautja profile, staff or the council manager only
    public static bool CanPurge(YautjaClanHallActor actor, YautjaClanMemberSnapshot target)
    {
        return IsManager(actor) && Outranks(actor, target);
    }
}

// clan sigils are the clan mask icons: style 1-20 in one of the five materials
public static class YautjaClanSigils
{
    public const int Styles = 20;

    public static readonly string[] Materials = ["ebony", "silver", "bronze", "crimson", "bone"];

    public const string Default = "pred_mask1_ebony";

    public static string Make(int style, string material)
    {
        return $"pred_mask{style}_{material}";
    }

    public static bool TryParse(string? sigil, out int style, out string material)
    {
        style = 0;
        material = string.Empty;
        if (string.IsNullOrEmpty(sigil) || !sigil.StartsWith("pred_mask"))
            return false;

        var rest = sigil["pred_mask".Length..];
        var split = rest.IndexOf('_');
        if (split <= 0 ||
            !int.TryParse(rest[..split], out style) ||
            style < 1 ||
            style > Styles)
        {
            return false;
        }

        material = rest[(split + 1)..];
        return Materials.Contains(material);
    }

    public static bool IsValid(string? sigil)
    {
        return TryParse(sigil, out _, out _);
    }

    public static string OrDefault(string? sigil)
    {
        return IsValid(sigil) ? sigil! : Default;
    }
}
