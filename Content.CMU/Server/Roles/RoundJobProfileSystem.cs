using Content.Server.AU14.Round;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared.CMU.Round;
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
    [Dependency] private CMURoundDirectorSystem _director = default!;
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

        AddForceComponents(
            results,
            job,
            profile.ID,
            profile.ForceComponents,
            profile.RemoveExisting);
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

        AddForceComponents(
            results,
            job,
            job.ID,
            job.RoundForceComponents,
            job.RoundComponentsRemoveExisting);
    }

    private void AddForceComponents(
        List<ResolvedRoundJobProfileComponents> results,
        JobPrototype job,
        string source,
        Dictionary<string, ComponentRegistry> registries,
        bool removeExisting)
    {
        if (TryGetCommittedForce(job, out var side, out var force))
        {
            AddComponentsForKey(results, registries, source, side.ToString(), removeExisting);

            var forceKey = force.Value;
            if (TryAddComponentsForKey(results, registries, source, forceKey, removeExisting))
                return;

            var legacyForceKey = forceKey.Equals("WEYU", StringComparison.OrdinalIgnoreCase)
                ? "WYPMC"
                : forceKey;
            if (!legacyForceKey.Equals(forceKey, StringComparison.OrdinalIgnoreCase))
                AddComponentsForKey(results, registries, source, legacyForceKey, removeExisting);

            return;
        }

        if (!string.IsNullOrWhiteSpace(job.RoundForce))
            AddComponentsForKey(results, registries, source, job.RoundForce, removeExisting);
    }

    private bool TryGetCommittedForce(
        JobPrototype job,
        out RoundSide side,
        out RoundForceId force)
    {
        var assignment = GetRoundSide(job) switch
        {
            RoundJobSide.Govfor => _director.Selection?.GovforAssignment,
            RoundJobSide.Opfor => _director.Selection?.OpforAssignment,
            _ => null,
        };

        if (assignment is { } committed)
        {
            side = committed.Side;
            force = committed.Force;
            return true;
        }

        side = default;
        force = default;
        return false;
    }

    private static void AddComponentsForKey(
        List<ResolvedRoundJobProfileComponents> results,
        Dictionary<string, ComponentRegistry> registries,
        string source,
        string key,
        bool removeExisting)
    {
        TryAddComponentsForKey(results, registries, source, key, removeExisting);
    }

    private static bool TryAddComponentsForKey(
        List<ResolvedRoundJobProfileComponents> results,
        Dictionary<string, ComponentRegistry> registries,
        string source,
        string key,
        bool removeExisting)
    {
        if (!TryGetComponents(registries, key, out var components))
            return false;

        results.Add(new ResolvedRoundJobProfileComponents(
            $"{source}:{key}",
            components,
            removeExisting));
        return true;
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
