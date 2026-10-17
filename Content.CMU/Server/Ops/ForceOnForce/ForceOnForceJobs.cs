using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ops.ForceOnForce;

/// <summary>
/// Shared GOVFOR/OPFOR mirroring for Force on Force. The dealing, overflow, mid-round balancer
/// and job scaling paths must map factions identically; they drifted once and dropped prefs.
/// </summary>
public static class FofJobs
{
    public const string Govfor = "GOVFOR";
    public const string Opfor = "OPFOR";

    /// <summary>
    /// Returns the other side's id for a faction job, or null for a non-faction id.
    /// </summary>
    public static string? Mirror(string jobId)
    {
        if (jobId.Contains(Govfor))
            return jobId.Replace(Govfor, Opfor);

        if (jobId.Contains(Opfor))
            return jobId.Replace(Opfor, Govfor);

        return null;
    }

    /// <summary>
    /// Maps job priorities onto one side: the other side's jobs are replaced by their mirrors
    /// and dropped when no mirror exists. Neutral jobs pass through when keepNeutral is set,
    /// otherwise they are dropped.
    /// </summary>
    public static Dictionary<ProtoId<JobPrototype>, JobPriority> MapSide(
        IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> priorities,
        string target,
        bool keepNeutral,
        IPrototypeManager prototypes)
    {
        var mapped = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
        foreach (var (id, priority) in priorities)
        {
            var jobId = id.Id;
            if (Mirror(jobId) is { } mirrored
                && !jobId.Contains(target))
            {
                jobId = mirrored;
                if (!prototypes.HasIndex<JobPrototype>(jobId))
                    continue; // no equivalent role on the target side
            }
            else if (!keepNeutral
                && !jobId.Contains(target))
                continue;

            mapped[new ProtoId<JobPrototype>(jobId)] = priority;
        }

        return mapped;
    }
}
