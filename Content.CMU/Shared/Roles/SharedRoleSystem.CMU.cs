using Content.Shared.Traits;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Roles;

public abstract partial class SharedRoleSystem
{
    private readonly Dictionary<string, (HashSet<JobRequirement>? Source, HashSet<JobRequirement>? Result)> _cmuTraitJobLimits = new();
    private int _cmuTraitCount = -1;

    /// <summary>
    ///     Adds a "can't have these traits" requirement for every trait whose <see cref="TraitPrototype.CMUAllowedJobs"/>
    ///     leaves this job out.
    /// </summary>
    private HashSet<JobRequirement>? CMUWithTraitJobLimits(JobPrototype job, HashSet<JobRequirement>? requirements)
    {
        var traitCount = ProtoMan.Count<TraitPrototype>();
        if (traitCount != _cmuTraitCount)
        {
            _cmuTraitJobLimits.Clear();
            _cmuTraitCount = traitCount;
        }

        if (_cmuTraitJobLimits.TryGetValue(job.ID, out var cached) && ReferenceEquals(cached.Source, requirements))
            return cached.Result;

        var banned = new HashSet<ProtoId<TraitPrototype>>();
        foreach (var trait in ProtoMan.EnumeratePrototypes<TraitPrototype>())
        {
            if (trait.CMUAllowedJobs is { } allowed && !allowed.Contains(job.ID))
                banned.Add(trait.ID);
        }

        var result = requirements;
        if (banned.Count > 0)
        {
            result = requirements == null ? new HashSet<JobRequirement>() : new HashSet<JobRequirement>(requirements);
            result.Add(new TraitsRequirement { Inverted = true, Traits = banned });
        }

        _cmuTraitJobLimits[job.ID] = (requirements, result);
        return result;
    }
}
