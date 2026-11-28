using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Threats;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flag;

public sealed partial class CMUFlagAuraSystem
{
    /// <summary>
    ///     The platoon sides. A listed reinforcement fighting for the other platoon isn't rallied.
    /// </summary>
    private static readonly HashSet<EntProtoId<IFFFactionComponent>> PlatoonSides = ["GOVFOR", "OPFOR"];

    /// <summary>
    ///     Jobs and ghost role mobs spawned by any third party, built on first use and dropped when prototypes reload.
    /// </summary>
    private HashSet<string>? _partyJobs;

    /// <summary>
    ///     Jobs and ghost role mobs each reinforcement list rallies, built on first use and dropped when prototypes
    ///     reload.
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _reinforcementJobs = new();

    private void InitializeReinforcements()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<ThirdPartyPrototype>() &&
            !args.WasModified<PartySpawnPrototype>() &&
            !args.WasModified<CMUFlagReinforcementsPrototype>())
        {
            return;
        }

        _partyJobs = null;
        _reinforcementJobs.Clear();
    }

    private CMUFlagReinforcementsPrototype? GetReinforcements(EntityUid? flag)
    {
        if (flag == null ||
            !_allegianceQuery.TryComp(flag, out var allegiance) ||
            allegiance.Reinforcements is not { } id)
        {
            return null;
        }

        return _prototypes.Index(id);
    }

    /// <summary>
    ///     Whether a marine with <paramref name="marineFactions"/> is rallied by a flag rallying
    ///     <paramref name="rallied"/>.
    /// </summary>
    /// <remarks>
    ///     Without a reinforcement list, anyone on a rallied side is. With one, the flag also rallies its nation's
    ///     reinforcements, wherever their IFF puts them, unless they fight for the other platoon. Every other third
    ///     party is left out, even on a rallied side.
    /// </remarks>
    private bool ShouldRally(
        EntityUid marine,
        HashSet<EntProtoId<IFFFactionComponent>> marineFactions,
        IReadOnlySet<EntProtoId<IFFFactionComponent>> rallied,
        CMUFlagReinforcementsPrototype? reinforcements)
    {
        var onSide = marineFactions.Overlaps(rallied);
        if (reinforcements == null)
            return onSide;

        // Party members are spawned either as a job or as a ghost role mob, so match on both.
        JobPrototype? job = null;
        if (_originalRoleQuery.TryComp(marine, out var role) && role.Job is { } jobId)
            _prototypes.TryIndex(jobId, out job);

        var mob = MetaData(marine).EntityPrototype?.ID;
        if (job?.RoundForce is { } force && reinforcements.RoundForces.Contains(force) ||
            IsListed(GetReinforcementJobs(reinforcements), job?.ID, mob))
        {
            return onSide || rallied.Count == 0 || !marineFactions.Overlaps(PlatoonSides);
        }

        return onSide && !IsListed(GetPartyJobs(), job?.ID, mob);
    }

    private static bool IsListed(HashSet<string> spawns, string? job, string? mob)
    {
        return job != null && spawns.Contains(job) || mob != null && spawns.Contains(mob);
    }

    private HashSet<string> GetReinforcementJobs(CMUFlagReinforcementsPrototype reinforcements)
    {
        if (_reinforcementJobs.TryGetValue(reinforcements.ID, out var jobs))
            return jobs;

        jobs = new HashSet<string>();
        foreach (var job in reinforcements.Jobs)
        {
            jobs.Add(job.Id);
        }

        foreach (var party in reinforcements.ThirdParties)
        {
            if (_prototypes.TryIndex(party, out var partyProto))
                AddPartyJobs(partyProto, jobs);
        }

        _reinforcementJobs[reinforcements.ID] = jobs;
        return jobs;
    }

    private HashSet<string> GetPartyJobs()
    {
        if (_partyJobs != null)
            return _partyJobs;

        _partyJobs = new HashSet<string>();
        foreach (var party in _prototypes.EnumeratePrototypes<ThirdPartyPrototype>())
        {
            AddPartyJobs(party, _partyJobs);
        }

        return _partyJobs;
    }

    private void AddPartyJobs(ThirdPartyPrototype party, HashSet<string> jobs)
    {
        if (!_prototypes.TryIndex(party.PartySpawn, out var spawn))
            return;

        AddSpawnJobs(spawn.LeadersToSpawn.Keys, jobs);
        AddSpawnJobs(spawn.GruntsToSpawn.Keys, jobs);
    }

    /// <summary>
    ///     Adds each spawn, which is either a job or a ghost role mob, and the job a ghost role mob hands out.
    /// </summary>
    private void AddSpawnJobs(IEnumerable<string> spawns, HashSet<string> jobs)
    {
        foreach (var spawn in spawns)
        {
            jobs.Add(spawn);

            var ev = new CMUGetGhostRoleJobEvent(spawn);
            RaiseLocalEvent(ref ev);
            if (ev.Job is { } job)
                jobs.Add(job);
        }
    }
}

/// <summary>
///     Asks for the job the ghost role mob <see cref="Mob"/> hands out, if it is one. Answered by the server, which
///     owns ghost roles.
/// </summary>
[ByRefEvent]
public record struct CMUGetGhostRoleJobEvent(string Mob, string? Job = null);
