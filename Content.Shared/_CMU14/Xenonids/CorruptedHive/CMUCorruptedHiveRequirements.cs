using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenonids.CorruptedHive;

/// <summary>
/// Keeps corrupted-hive access aligned with the normal Threat Leader requirement.
/// </summary>
public static class CMUCorruptedHiveRequirements
{
    public static readonly TimeSpan RequiredThreatMemberTime = TimeSpan.FromHours(5);

    public static bool IsEligible(
        IEntityManager entityManager,
        IPrototypeManager prototypes,
        ISharedPlaytimeManager playtime,
        ICommonSession session)
    {
        var requirement = new RoleTimeRequirement
        {
            Role = "AUJobThreatMember",
            Time = RequiredThreatMemberTime,
        };

        return requirement.Check(
            entityManager,
            prototypes,
            null,
            playtime.GetPlayTimes(session),
            out _);
    }
}
