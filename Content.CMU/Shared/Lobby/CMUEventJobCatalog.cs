using System.Linq;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Lobby;

/// <summary>The preference roster, excluding hidden faction and equipment variants of the same jobs.</summary>
public static class CMUEventJobCatalog
{
    public static List<JobPrototype> GetJobs(IPrototypeManager prototypes)
    {
        var roster = prototypes.EnumeratePrototypes<DepartmentPrototype>()
            .Where(department => !department.EditorHidden)
            .SelectMany(department => department.Roles)
            .ToHashSet();
        return prototypes.EnumeratePrototypes<JobPrototype>()
            .Where(job => job.RoundSide != RoundJobSide.None &&
                ((!job.Hidden && job.SetPreference) ||
                 // OPFOR's canonical counterparts inherit hidden preference flags; FoF shares their priorities.
                 job.RoundSide == RoundJobSide.Opfor && roster.Contains(job.ID)))
            .OrderBy(job => job.ID)
            .ToList();
    }

    public static string PreferenceRole(JobPrototype job) => job.ForceOnForcePreferenceRole ?? job.RoundRole ?? job.ID;
}
