using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// CMU14: the single authority for who may open a Yautja gear rack. The client runs this to predict the
/// denial, so the UI does not open for a moment before the server refuses, and the server runs it to
/// enforce - meaning the two have to agree exactly. Keeping it in one place is what stops them drifting:
/// the client used to go by access tags alone, so a Yautja carrying no ID was denied locally and then
/// let in by the rank-based server check.
/// </summary>
public sealed partial class YautjaGearRackAccessSystem : EntitySystem
{
    private static readonly ProtoId<AccessLevelPrototype> YautjaSecureAccess = "CMUAccessYautjaSecure";
    private static readonly ProtoId<AccessLevelPrototype> YautjaElderAccess = "CMUAccessYautjaElder";
    private static readonly ProtoId<AccessLevelPrototype> YautjaAncientAccess = "CMUAccessYautjaAncient";
    private static readonly ProtoId<AccessLevelPrototype> YautjaBadBloodAccess = "CMUAccessYautjaBadBlood";
    private static readonly ProtoId<JobPrototype> HunterJob = "CMUYautjaHunter";
    private static readonly ProtoId<JobPrototype> YoungbloodJob = "CMUYautjaYoungblood";
    private static readonly ProtoId<JobPrototype> AncientJob = "CMUYautjaAncient";

    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    // CMU14 method: the loc key explaining why this user may not open this rack, or null when they may.
    public string? GetOpenDenial(EntityUid user, YautjaGearRackKind kind)
    {
        return kind switch
        {
            YautjaGearRackKind.Adult => DenyIfMissingAccessThenWrongRole(
                HasRackAccess(user, YautjaRank.Blooded),
                IsAdultHunter(user)),
            YautjaGearRackKind.Youngblood => DenyIfMissingAccessThenWrongRole(
                HasRackAccess(user, YautjaRank.YoungBlood),
                HasJob(user, YoungbloodJob) || IsAdultHunter(user)),
            YautjaGearRackKind.Elder => DenyIfMissingAccessThenWrongRole(
                HasRackAccess(user, YautjaRank.Elder),
                IsAdultHunter(user)),
            YautjaGearRackKind.Thrall => HasComp<YautjaThrallComponent>(user)
                ? null
                : "cm-vending-machine-access-denied",
            YautjaGearRackKind.BloodedThrall => HasComp<YautjaTechAuthorizedComponent>(user)
                ? null
                : "cm-vending-machine-access-denied",
            YautjaGearRackKind.BadBlood => HasAccess(user, YautjaBadBloodAccess)
                ? null
                : "cm-vending-machine-access-denied",
            YautjaGearRackKind.Stranded => HasRackAccess(user, YautjaRank.Blooded) &&
                                            !HasAccess(user, YautjaBadBloodAccess)
                ? null
                : "cm-vending-machine-access-denied",
            _ => null,
        };
    }

    private static string? DenyIfMissingAccessThenWrongRole(bool hasAccess, bool hasRole)
    {
        if (!hasAccess)
            return "cm-vending-machine-access-denied";

        return hasRole ? null : "cmu-yautja-rack-wrong-role";
    }

    private bool HasAccess(EntityUid user, ProtoId<AccessLevelPrototype> access)
    {
        // CMU14: a Yautja carries their access on the bracer chip, so recognise the badges directly.
        if (access == YautjaBadBloodAccess && HasComp<YautjaBadBloodComponent>(user))
            return true;

        var tags = _accessReader.FindAccessTags(user);
        return tags.Contains(access);
    }

    private bool HasRackAccess(EntityUid user, YautjaRank rank)
    {
        // CMU14: no ID card required - a Yautja of the rack's rank or above may open it. YautjaRank is
        // ordered by seniority, so an elder opens the adult rack too.
        if (TryComp<YautjaComponent>(user, out var yautja) && yautja.ClanRank >= rank)
            return true;

        foreach (var access in YautjaRankMetadata.GetRackAccessTags(rank))
        {
            if (HasAccess(user, access))
                return true;
        }

        return false;
    }

    // the ancient slot is its own job so it can skip the hunt cap, but it's a hunter in every other way.
    // without this every rack told ancients they had the wrong role
    private bool IsAdultHunter(EntityUid user)
    {
        return HasJob(user, HunterJob) || HasJob(user, AncientJob);
    }

    private bool HasJob(EntityUid user, ProtoId<JobPrototype> job)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;

        if (_job.MindHasJobWithId(mindId, job.Id))
            return true;

        // CMU14: a Yautja spawned from the spawn panel has no job at all, so the role half of the rack check rejected
        // them even though the rank check above had already granted access. Jobless Yautja count as the right role.
        return HasComp<YautjaComponent>(user) &&
               !HasComp<YautjaYoungbloodComponent>(user) &&
               !_job.MindTryGetJobId(mindId, out _);
    }
}
