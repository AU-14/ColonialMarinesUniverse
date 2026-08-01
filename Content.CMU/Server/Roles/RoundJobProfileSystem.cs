using Content.Shared._CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Roles;

public readonly record struct ResolvedRoundJobProfileComponents(
    string Source,
    ComponentRegistry Components,
    bool RemoveExisting);

public sealed partial class RoundJobProfileSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("au14.round_job_profiles");

    public RoundJobSide GetRoundSide(JobPrototype? job, string? fallbackJobId = null)
    {
        if (job?.RoundSide is { } side && side != RoundJobSide.None)
            return side;

        var jobId = fallbackJobId ?? job?.ID;
        if (string.IsNullOrWhiteSpace(jobId))
            return RoundJobSide.None;

        if (jobId.Contains("OPFOR", StringComparison.OrdinalIgnoreCase))
            return RoundJobSide.Opfor;

        if (jobId.Contains("GOVFOR", StringComparison.OrdinalIgnoreCase))
            return RoundJobSide.Govfor;

        return RoundJobSide.None;
    }

    public List<ResolvedRoundJobProfileComponents> GetProfileComponents(JobPrototype job)
    {
        var results = new List<ResolvedRoundJobProfileComponents>();

        foreach (var profileId in job.RoundProfiles)
        {
            if (!_prototypes.TryIndex(profileId, out RoundJobProfilePrototype? profile))
            {
                _sawmill.Error($"Job '{job.ID}' references missing round job profile '{profileId}'.");
                continue;
            }

            AddProfileComponents(results, job, profile);
        }

        AddInlineJobComponents(results, job);
        return results;
    }

    private void AddProfileComponents(
        List<ResolvedRoundJobProfileComponents> results,
        JobPrototype job,
        RoundJobProfilePrototype profile)
    {
        if (profile.Components.Count > 0)
        {
            results.Add(new ResolvedRoundJobProfileComponents(
                profile.ID,
                profile.Components,
                profile.RemoveExisting));
        }

        var side = GetRoundSide(job);
        if (side != RoundJobSide.None &&
            TryGetComponents(profile.SideComponents, side.ToString(), out var sideComponents))
        {
            results.Add(new ResolvedRoundJobProfileComponents(
                $"{profile.ID}:{side}",
                sideComponents,
                profile.RemoveExisting));
        }

        if (!string.IsNullOrWhiteSpace(job.RoundForce) &&
            TryGetComponents(profile.ForceComponents, job.RoundForce, out var forceComponents))
        {
            results.Add(new ResolvedRoundJobProfileComponents(
                $"{profile.ID}:{job.RoundForce}",
                forceComponents,
                profile.RemoveExisting));
        }
    }

    private void AddInlineJobComponents(
        List<ResolvedRoundJobProfileComponents> results,
        JobPrototype job)
    {
        if (job.RoundComponents.Count > 0)
        {
            results.Add(new ResolvedRoundJobProfileComponents(
                job.ID,
                job.RoundComponents,
                job.RoundComponentsRemoveExisting));
        }

        var side = GetRoundSide(job);
        if (side != RoundJobSide.None &&
            TryGetComponents(job.RoundSideComponents, side.ToString(), out var sideComponents))
        {
            results.Add(new ResolvedRoundJobProfileComponents(
                $"{job.ID}:{side}",
                sideComponents,
                job.RoundComponentsRemoveExisting));
        }

        if (!string.IsNullOrWhiteSpace(job.RoundForce) &&
            TryGetComponents(job.RoundForceComponents, job.RoundForce, out var forceComponents))
        {
            results.Add(new ResolvedRoundJobProfileComponents(
                $"{job.ID}:{job.RoundForce}",
                forceComponents,
                job.RoundComponentsRemoveExisting));
        }
    }

    public bool ApplyJobProfile(EntityUid target, JobPrototype job)
    {
        if (HasComp<RoundJobProfileAppliedComponent>(target))
            return false;

        var applied = false;
        foreach (var profile in GetProfileComponents(job))
        {
            EntityManager.AddComponents(target, profile.Components, profile.RemoveExisting);
            applied = true;
        }

        if (applied)
            EnsureComp<RoundJobProfileAppliedComponent>(target);

        return applied;
    }

    private static bool TryGetComponents(
        Dictionary<string, ComponentRegistry> registries,
        string key,
        out ComponentRegistry components)
    {
        foreach (var (name, registry) in registries)
        {
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            components = registry;
            return true;
        }

        components = default!;
        return false;
    }

}
