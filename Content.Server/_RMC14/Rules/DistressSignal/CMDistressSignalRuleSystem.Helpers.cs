using System.Diagnostics.CodeAnalysis;
using Content.Server.Power.Components;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Item;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Thunderdome;
using Content.Shared._RMC14.WeedKiller;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.FloorResin;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared.Coordinates;
using Content.Shared.Fax.Components;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Rules.DistressSignal;

public sealed partial class CMDistressSignalRuleSystem
{
    private const int FaxPowerLoadValue = 5;

    private readonly Dictionary<ResPath, Queue<Entity<MapComponent>>> _preloadedAdminMaps = new();

    /// <summary>
    /// Checks whether a player is allowed to play the specified job, considering bans and playtime requirements.
    /// </summary>
    private bool IsJobAllowed(NetUserId id, ProtoId<JobPrototype> role)
    {
        if (!_player.TryGetSessionById(id, out var player))
            return false;

        var jobBans = _bans.GetJobBans(player.UserId);
        if (jobBans != null && jobBans.Contains(role))
            return false;

        return _playTime.IsAllowed(player, role);
    }

    // TODO RMC14: Move these to a prototype
    private string GetRandomOperationName()
    {
        if (_usingCustomOperationName && OperationName != null)
        {
            _usingCustomOperationName = false;
            return OperationName;
        }

        var name = string.Empty;
        if (_operationNames.Count > 0)
            name += $"{_random.Pick(_operationNames)} ";

        if (_operationPrefixes.Count > 0)
            name += $"{_random.Pick(_operationPrefixes)}";

        if (_operationSuffixes.Count > 0)
            name += $"-{_random.Pick(_operationSuffixes)}";

        return name.Trim();
    }

    // TODO RMC14 this would be literally anywhere else if the code for loading maps wasn't dogshit and broken upstream
    private void SpawnAdminAreas(CMDistressSignalRuleComponent comp)
    {
        bool SpawnMap(ResPath path, [NotNullWhen(true)] out EntityUid? mapEntityUid)
        {
            mapEntityUid = default;

            try
            {
                if (string.IsNullOrWhiteSpace(path.ToString()))
                    return false;

                if (TryTakePreloadedAdminMap(path, out var preloadedMap))
                {
                    _mapSystem.InitializeMap(preloadedMap.Owner);
                    mapEntityUid = preloadedMap.Owner;
                    return true;
                }

                if (!_mapLoader.TryLoadMap(path, out var map, out _))
                    return false;

                _mapSystem.InitializeMap((map.Value, map.Value));
                mapEntityUid = map;
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Error loading {path} map:\n{e}");
            }

            return false;
        }

        foreach (var map in comp.AuxiliaryMaps)
        {
            SpawnMap(new ResPath(map), out _);
        }

        if (SpawnMap(comp.Thunderdome, out var mapEnt))
            EnsureComp<ThunderdomeMapComponent>(mapEnt.Value);
    }

    private void PreloadAdminAreas()
    {
        ClearPreloadedAdminMaps();

        foreach (var rule in GameTicker.GetAddedGameRules<CMDistressSignalRuleComponent>())
        {
            foreach (var map in rule.Comp.AuxiliaryMaps)
            {
                PreloadAdminMap(new ResPath(map));
            }

            PreloadAdminMap(rule.Comp.Thunderdome);
        }
    }

    private void PreloadAdminMap(ResPath path)
    {
        if (string.IsNullOrWhiteSpace(path.ToString()))
            return;

        if (!_mapLoader.TryLoadMap(path, out var map, out _))
        {
            Log.Error($"Failed to preload admin map {path}");
            return;
        }

        if (!_preloadedAdminMaps.TryGetValue(path, out var maps))
        {
            maps = new Queue<Entity<MapComponent>>();
            _preloadedAdminMaps.Add(path, maps);
        }

        maps.Enqueue(map.Value);
    }

    private bool TryTakePreloadedAdminMap(ResPath path, out Entity<MapComponent> map)
    {
        if (_preloadedAdminMaps.TryGetValue(path, out var maps))
        {
            while (maps.TryDequeue(out var preloaded))
            {
                if (!TerminatingOrDeleted(preloaded))
                {
                    if (maps.Count == 0)
                        _preloadedAdminMaps.Remove(path);

                    map = preloaded;
                    return true;
                }
            }

            _preloadedAdminMaps.Remove(path);
        }

        map = default;
        return false;
    }

    private void ClearPreloadedAdminMaps()
    {
        foreach (var maps in _preloadedAdminMaps.Values)
        {
            foreach (var map in maps)
            {
                if (!TerminatingOrDeleted(map))
                    QueueDel(map);
            }
        }

        _preloadedAdminMaps.Clear();
    }

    private void SetCamoType(CamouflageType? ct = null)
    {
        if (ct != null)
        {
            _camo.CurrentMapCamouflage = ct.Value;
            return;
        }

        if (SelectedPlanetMap != null)
            _camo.CurrentMapCamouflage = SelectedPlanetMap.Value.Comp.Camouflage;
    }

    /// <summary>
    /// Sets the hive of all loaded xeno friendly entities (e.g., weeds).
    /// Only makes sense for distress signal with 1 hive, with multiple hives you would need to determine which weeds belong to which hive
    /// </summary>
    private void SetFriendlyHives(EntityUid hive)
    {
        var query = EntityQueryEnumerator<XenoFriendlyComponent>();
        while (query.MoveNext(out var weeds, out _))
        {
            _hive.SetHive(weeds, hive);
        }

        var resinSlowdown = EntityQueryEnumerator<ResinSlowdownModifierComponent>();
        while (resinSlowdown.MoveNext(out var uid, out _))
        {
            _hive.SetHive(uid, hive);
        }

        var resinSpeedup = EntityQueryEnumerator<ResinSpeedupModifierComponent>();
        while (resinSpeedup.MoveNext(out var uid, out _))
        {
            _hive.SetHive(uid, hive);
        }

        var tunnelQuery = EntityQueryEnumerator<XenoTunnelComponent>();
        var tunnels = new List<EntityUid>();

        while (tunnelQuery.MoveNext(out var ent, out _))
        {
            tunnels.Add(ent);
        }
        
        foreach (var tunnel in tunnels)
        {
            // Replace all pre-mapped tunnels with a new tunnel with name and associated with the hive
            if (_xenoTunnel.TryPlaceTunnel(hive, null, tunnel.ToCoordinates(), out var newTunnel))
                RemCompDeferred<DeletedByWeedKillerComponent>(newTunnel.Value);

            QueueDel(tunnel);
        }
    }

    private void UnpowerFaxes(MapId map)
    {
        var faxes = EntityQueryEnumerator<FaxMachineComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (faxes.MoveNext(out _, out var power, out var xform))
        {
            if (xform.MapID != map)
                continue;

            power.Load = FaxPowerLoadValue;
            power.NeedsPower = true;
        }
    }

    private bool IsChamberFull(EntityUid chamber)
    {
        if (!_hyperSleepChamberQuery.TryComp(chamber, out var hyperSleep))
            return false;

        return _containers.TryGetContainer(chamber, hyperSleep.ContainerId, out var container) &&
               container.Count > 0;
    }

    /// <summary>
    /// Collects and categorizes all squad-based spawn targets by squad and job.
    /// </summary>
    private void CollectSquadSpawners(Spawners spawners)
    {
        var squadQuery = EntityQueryEnumerator<SquadSpawnerComponent>();
        while (squadQuery.MoveNext(out var uid, out var spawner))
        {
            var target = uid;
            if (!IsChamberFull(uid) && TryFindAttachedChamber(uid, out var attachedChamber))
                target = attachedChamber;

            if (spawner.Role == null)
                spawners.SquadAny.GetOrNew(spawner.Squad).Add(target);
            else
                spawners.Squad.GetOrNew(spawner.Squad).GetOrNew(spawner.Role.Value).Add(target);
        }
    }

    private bool TryFindAttachedChamber(EntityUid spawner, out EntityUid chamber)
    {
        chamber = default;
        foreach (var cardinal in _rmcMap.CardinalDirections)
        {
            var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(spawner, cardinal);
            while (anchored.MoveNext(out var anchoredId))
            {
                if (_hyperSleepChamberQuery.HasComp(anchoredId))
                {
                    chamber = anchoredId;
                    return true;
                }
            }
        }
        return false;
    }

    private Spawners GetSpawners()
    {
        var spawners = new Spawners();
        CollectSquadSpawners(spawners);
        var spawnPoints = EntityQueryEnumerator<SpawnPointComponent>();
        while (spawnPoints.MoveNext(out var uid, out var spawnPoint))
        {
            spawners.AddSpawnPoint(uid, spawnPoint.Job, spawnPoint.SpawnType);
        }

        return spawners;
    }

    private (EntProtoId Id, EntityUid Ent) NextSquad(
        ProtoId<JobPrototype> job,
        CMDistressSignalRuleComponent rule,
        EntProtoId<SquadTeamComponent>? preferred)
    {
        var squads = new List<(EntProtoId SquadId, EntityUid Squad, int Players)>();
        foreach (var (squadId, squad) in rule.Squads)
        {
            var players = 0;
            if (TryComp(squad, out SquadTeamComponent? team))
            {
                var roles = team.Roles;
                var maxRoles = team.MaxRoles;
                if (roles.TryGetValue(job, out var currentPlayers))
                    players = currentPlayers;

                if (preferred != null &&
                    preferred == squadId &&
                    (!maxRoles.TryGetValue(job, out var max) || players < max))
                {
                    return (squadId, squad);
                }
            }

            squads.Add((squadId, squad, players));
        }

        _random.Shuffle(squads);
        squads.Sort((a, b) => a.Players.CompareTo(b.Players));

        var chosen = squads[0];
        return (chosen.SquadId, chosen.Squad);
    }

    /// <summary>
    /// Finds the best spawn point for a player based on their job, squad preference, and availability.
    /// Falls back to generic spawn points if preferred ones are unavailable.
    /// </summary>
    private (EntityUid Spawner, EntityUid? Squad)? GetSpawner(
        CMDistressSignalRuleComponent rule,
        JobPrototype job,
        EntProtoId<SquadTeamComponent>? preferred)
    {
        // CMU14: The shared cache owns the lifetime of this derived Distress lookup.
        var allSpawners = _roundSpawnPoints.Active && _roundStartSpawners is { } cached
            ? cached
            : GetSpawners();
        EntityUid? squad = null;
        EntProtoId? squadId = null;

        if (job.HasSquad)
        {
            var (nextSquadId, squadEnt) = NextSquad(job, rule, preferred);
            squadId = nextSquadId;
            squad = squadEnt;

            if (allSpawners.Squad.TryGetValue(nextSquadId, out var jobSpawners) &&
                jobSpawners.TryGetValue(job.ID, out var spawners) &&
                TryPickSpawner(spawners, availableOnly: true, out var spawner))
            {
                return (spawner, squadEnt);
            }

            if (allSpawners.SquadAny.TryGetValue(nextSquadId, out var anySpawners) &&
                TryPickSpawner(anySpawners, availableOnly: true, out spawner))
            {
                return (spawner, squadEnt);
            }

            if (jobSpawners != null &&
                jobSpawners.TryGetValue(job.ID, out spawners) &&
                TryPickSpawner(spawners, availableOnly: false, out spawner))
            {
                return (spawner, squadEnt);
            }

            if (anySpawners != null &&
                TryPickSpawner(anySpawners, availableOnly: false, out spawner))
            {
                return (spawner, squadEnt);
            }

            Log.Debug($"No squad spawn found for player. Falling back to generic spawn points. Squad: {nextSquadId}, job: {job.ID}");

            if (allSpawners.NonSquad.TryGetValue(job.ID, out spawners) &&
                TryPickSpawner(spawners, availableOnly: true, out spawner))
            {
                return (spawner, squadEnt);
            }

            if (spawners != null &&
                TryPickSpawner(spawners, availableOnly: false, out spawner))
            {
                return (spawner, squadEnt);
            }
        }
        else
        {
            if (allSpawners.NonSquad.TryGetValue(job.ID, out var spawners) &&
                TryPickSpawner(spawners, availableOnly: true, out var spawner))
            {
                return (spawner, null);
            }

            if (spawners != null &&
                TryPickSpawner(spawners, availableOnly: false, out spawner))
            {
                return (spawner, null);
            }

            Log.Debug($"No job-specific spawn found for player. Falling back to generic spawn points. Job: {job.ID}");
        }

        if (allSpawners.JobPoints.TryGetValue(job.ID, out var jobPoints) &&
            TryPickSpawner(jobPoints, availableOnly: false, out var fallback))
        {
            return (fallback, squad);
        }

        if (TryPickSpawner(allSpawners.AllJobPoints, availableOnly: false, out fallback))
            return (fallback, squad);

        if (TryPickSpawner(allSpawners.LatePoints, availableOnly: false, out fallback))
            return (fallback, squad);

        if (squadId is { } failedSquad)
            Log.Error($"No valid spawn found for player. Squad: {failedSquad}, job: {job.ID}");
        else
            Log.Error($"No valid spawn found for player. Job: {job.ID}");

        return null;
    }

    private bool TryPickSpawner(
        IReadOnlyList<EntityUid> spawners,
        bool availableOnly,
        out EntityUid spawner)
    {
        _spawnPointCandidates.Clear();
        foreach (var candidate in spawners)
        {
            if (TerminatingOrDeleted(candidate) ||
                (TryComp(candidate, out MetaDataComponent? meta) && meta.EntityPaused) ||
                (availableOnly && IsChamberFull(candidate)))
            {
                continue;
            }

            _spawnPointCandidates.Add(candidate);
        }

        if (_spawnPointCandidates.Count == 0)
        {
            spawner = default;
            return false;
        }

        spawner = _random.Pick(_spawnPointCandidates);
        return true;
    }

    private void ReloadPrototypes()
    {
        _operationNames.Clear();
        _operationPrefixes.Clear();
        _operationSuffixes.Clear();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.TryComp(out RMCDistressSignalNamesComponent? names, _compFactory))
                _operationNames.UnionWith(names.Names);

            if (prototype.TryComp(out RMCDistressSignalPrefixesComponent? prefixes, _compFactory))
                _operationPrefixes.UnionWith(prefixes.Prefixes);

            if (prototype.TryComp(out RMCDistressSignalSuffixesComponent? suffixes, _compFactory))
                _operationSuffixes.UnionWith(suffixes.Suffixes);
        }
    }

    /// <summary>
    /// Container class for organizing structural spawn candidates for one player-spawn batch.
    /// Chamber availability is checked live because earlier serial spawns can fill them.
    /// </summary>
    private sealed class Spawners
    {
        public readonly Dictionary<EntProtoId, Dictionary<ProtoId<JobPrototype>, List<EntityUid>>> Squad = new();
        public readonly Dictionary<EntProtoId, List<EntityUid>> SquadAny = new();
        public readonly Dictionary<ProtoId<JobPrototype>, List<EntityUid>> NonSquad = new();
        public readonly Dictionary<ProtoId<JobPrototype>, List<EntityUid>> JobPoints = new();
        public readonly List<EntityUid> AllJobPoints = [];
        public readonly List<EntityUid> LatePoints = [];

        public void AddSpawnPoint(
            EntityUid uid,
            ProtoId<JobPrototype>? job,
            SpawnPointType spawnType)
        {
            if (job is { } jobId)
                NonSquad.GetOrNew(jobId).Add(uid);

            if (spawnType == SpawnPointType.Job)
            {
                AllJobPoints.Add(uid);
                if (job is { } pointJob)
                    JobPoints.GetOrNew(pointJob).Add(uid);
            }

            if (spawnType == SpawnPointType.LateJoin)
                LatePoints.Add(uid);
        }
    }
}
