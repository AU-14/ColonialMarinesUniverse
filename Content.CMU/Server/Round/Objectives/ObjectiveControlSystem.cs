using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Round.Objectives;
using Content.Shared.Maps;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Objectives;

public sealed partial class ObjectiveControlSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSystem = default!;
    [Dependency] private ObjectiveInterestSystem _interest = default!;

    private readonly List<(EntityUid Uid, CMUObjectiveComponent Comp)> _allObjectives = new();
    private EntityUid _objectiveMasterUid = EntityUid.Invalid;
    private MapId _planetMapId = MapId.Nullspace;
    private ISawmill _logs = default!;

    /// <summary>True once a winning final objective has been activated.</summary>
    public bool IsWinActive { get; set; }

    /// <summary>True once Main() has rolled this round's objectives.</summary>
    public bool SelectionComplete => GetOrReselectObjMaster()?.SelectionComplete ?? false;
    public MapId? GetPlanetMapId() => _planetMapId;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("objectives");
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<CMUObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<CMUObjectiveComponent, ComponentShutdown>(OnObjectiveShutdown);
        SubscribeLocalEvent<SpendWinPointsEvent>(OnSpendWinPoints);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _planetMapId = MapId.Nullspace;
        _objectiveMasterUid = EntityUid.Invalid;
        _allObjectives.Clear();
        _fetchObjectiveByItem = null;
    }

    private void OnObjectiveShutdown(EntityUid uid, CMUObjectiveComponent component, ref ComponentShutdown args)
    {
        _allObjectives.RemoveAll(o => o.Uid == uid);
        _interest.UnregisterInterest(uid);
    }

    private void OnObjectiveStartup(EntityUid uid, CMUObjectiveComponent component, ref ComponentStartup args)
    {
        _logs.Debug($"[OBJ START] CMUObjectiveComponent started: [{ToPrettyString(uid)}]");
        _allObjectives.Add((uid, component));
        InitializeObjectiveStatuses(component);

        if (TryComp(_objectiveMasterUid, out CMUObjectiveMasterComponent? master) && master.SelectionComplete)
            Timer.Spawn(0, () => TryLateActivateObjective(uid));
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        IsWinActive = false;
        var gameMap = ev.GameMap;
        var map = ev.Map;
        var grids = ev.Grids.ToArray();

        Timer.Spawn(0, () => SetupPostGameMapLoad(gameMap, map, grids));
    }

    private void SetupPostGameMapLoad(GameMapPrototype gameMap, MapId mapId, IReadOnlyList<EntityUid> grids)
    {
        var presetId = _gameTicker.Preset?.ID;
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var selectedPlanet = _auRoundSystem.GetSelectedPlanet();
        if (selectedPlanet == null
            || !gameMap.ID.Equals(selectedPlanet.MapId, StringComparison.OrdinalIgnoreCase))
        {
            _logs.Debug($"[OBJ-CTRL] OnPostGameMapLoad: map '{gameMap.ID}' is not the voted planet '{selectedPlanet?.MapId}', skipping.");
            return;
        }

        EntityUid? bestPlanetGrid = null;
        float bestArea = -1f;
        foreach (var grid in grids)
        {
            if (!TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var area = gridComp.LocalAABB.Width * gridComp.LocalAABB.Height;
            if (!(area > bestArea))
                continue;

            bestArea = area;
            bestPlanetGrid = grid;
        }

        if (bestPlanetGrid == null)
        {
            _logs.Warning($"[OBJ-CTRL] OnPostGameMapLoad: planet map has no valid grids!");
            return;
        }

        _planetMapId = mapId;
        EnsureComp<RMCPlanetComponent>(_mapSystem.GetMap(mapId));

        SpawnMissingCatalogObjectives(bestPlanetGrid.Value, mapId, presetId);

        bool hasPlanetMaster = false;
        var masterScan = EntityQueryEnumerator<CMUObjectiveMasterComponent, TransformComponent>();
        while (masterScan.MoveNext(out var mUid, out _, out var mXform))
        {
            if (mXform.MapID != mapId)
                continue;

            hasPlanetMaster = true;
            _objectiveMasterUid = mUid;
            break;
        }

        if (hasPlanetMaster)
        {
            _logs.Debug($"[OBJ-CTRL] SetupPostGameMapLoad: ObjectiveMaster loaded from planet, running Main()");
            ActivateObjectiveMaster();
            Timer.Spawn(0, Main);
            return;
        }

        bool spawnedIn = false;
        var compFactory = EntityManager.ComponentFactory;
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryComp<CMUObjectiveMasterComponent>(out var masterComp, compFactory)
                    || !string.Equals(masterComp.GamePreset, presetId, StringComparison.OrdinalIgnoreCase))
                continue;

            _objectiveMasterUid = Spawn(proto.ID, new EntityCoordinates(bestPlanetGrid.Value, Vector2.Zero));
            spawnedIn = true;
            _logs.Warning($"[OBJ-CTRL] SetupPostGameMapLoad: auto-spawned missing ObjectiveMaster '{proto.ID}' for preset '{presetId}'");
            break;
        }

        if (!spawnedIn)
        {
            _objectiveMasterUid = Spawn("ObjectiveMasterBaseDistress", new EntityCoordinates(bestPlanetGrid.Value, Vector2.Zero));
            _logs.Warning($"[OBJ-CTRL] SetupPostGameMapLoad: no master found for preset '{presetId}', spawned fallback 'ObjectiveMasterBaseDistress'");
        }

        ActivateObjectiveMaster();
        Timer.Spawn(0, Main);
    }

    private void ActivateObjectiveMaster()
    {
        if (!TryComp(_objectiveMasterUid, out CMUObjectiveMasterComponent? master)) return;
        master.IsActive = true;
        DirtyObjectiveMaster();
    }

    private void OnSpendWinPoints(SpendWinPointsEvent ev)
    {
        if (string.IsNullOrEmpty(ev.Team) || ev.Team == "none")
            return;

        if (GetOrReselectObjMaster() is not { } master)
        {
            _logs.Error("[OBJ-CTRL] OnSpendWinPoints called with null ObjectiveMaster!");
            return;
        }

        var key = ev.Team.ToLowerInvariant();
        var data = master.GetOrCreateFactionData(key);
        data.CurrentWinPoints = Math.Max(0, data.CurrentWinPoints - ev.Amount);
        DirtyObjectiveMaster();
    }

    private CMUObjectiveMasterComponent? GetOrReselectObjMaster()
    {
        if (_objectiveMasterUid.IsValid() && TryComp(_objectiveMasterUid, out CMUObjectiveMasterComponent? master))
            return master;

        var query = EntityQueryEnumerator<CMUObjectiveMasterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != _planetMapId)
                continue;

            _objectiveMasterUid = uid;
            return comp;
        }

        _logs.Debug("[OBJ-CTRL] GetOrReselectObjMaster: no master found.");
        return null;
    }

    private void DirtyObjectiveMaster()
    {
        if (_objectiveMasterUid.IsValid() && TryComp(_objectiveMasterUid, out CMUObjectiveMasterComponent? master))
            Dirty(_objectiveMasterUid, master);
    }

    public (int current, int required) GetWinPoints(string faction)
    {
        if (GetOrReselectObjMaster() is not { } master)
            return (0, 0);

        var key = faction.ToLowerInvariant();
        var data = master.GetOrCreateFactionData(key);
        return (data.CurrentWinPoints, data.RequiredWinPoints);
    }

    private Dictionary<string, string>? _fetchObjectiveByItem;

    public bool TryGetFetchObjectiveForItem(string itemProto, out string objectiveProto)
    {
        if (_fetchObjectiveByItem == null)
        {
            _fetchObjectiveByItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var compFactory = EntityManager.ComponentFactory;
            foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (!proto.TryComp<FetchObjectiveComponent>(out var fetch, compFactory)
                        || string.IsNullOrEmpty(fetch.TargetPrototype)
                        || !proto.TryComp<CMUObjectiveComponent>(out _, compFactory))
                    continue;

                _fetchObjectiveByItem.TryAdd(fetch.TargetPrototype, proto.ID);
            }
        }

        if (_fetchObjectiveByItem.TryGetValue(itemProto, out var found))
        {
            objectiveProto = found;
            return true;
        }

        objectiveProto = string.Empty;
        return false;
    }

    public void LateSpawnFetchObjectiveForItem(EntityUid itemUid, string objectiveProto)
    {
        var itemProto = Comp<MetaDataComponent>(itemUid).EntityPrototype?.ID ?? string.Empty;

        foreach (var (uid, comp) in _allObjectives)
        {
            if (!Exists(uid) || comp.Active || Transform(uid).MapID != _planetMapId)
                continue;

            if (!TryComp(uid, out FetchObjectiveComponent? fetch)
                    || !string.Equals(fetch.TargetPrototype, itemProto, StringComparison.OrdinalIgnoreCase))
                continue;

            EnsureComp<FetchItemComponent>(itemUid).ObjectiveUid = uid;
            ActivateObjective(uid, comp,
                comp.FactionNeutral || comp.Factions.Count == 0 ? null : comp.Factions[0],
                lateActivation: true);
            _logs.Info($"[OBJ-LATE] Item '{itemProto}' activated existing objective '{comp.ObjectiveDescription}'.");
            return;
        }

        var tracker = Spawn(objectiveProto, Transform(itemUid).Coordinates);
        EnsureComp<FetchItemComponent>(itemUid).ObjectiveUid = tracker;
        _logs.Info($"[OBJ-LATE] Item '{itemProto}' spawned its objective '{objectiveProto}' beside it ({ToPrettyString(tracker)}).");
    }

    private void Main()
    {
        if (GetOrReselectObjMaster() is not { } master)
            return;

        var presetId = _gameTicker.Preset?.ID.ToLowerInvariant() ?? string.Empty;
        var modeObjectives = GetInactiveObjectives(presetId, Transform(_objectiveMasterUid).MapID);
        _logs.Info($"[OBJ-CTRL] Main(): Preset='{presetId}', Eligible objectives={modeObjectives.Count}");

        if (modeObjectives.Count == 0)
        {
            _logs.Warning($"[OBJ-CTRL] Main(): No objectives passed filtering for preset '{presetId}':");
            foreach (var (_, comp) in _allObjectives.Take(30))
                _logs.Warning($"   {comp.ObjectiveDescription} - active={comp.Active} - neutral={comp.FactionNeutral} - presets=[{string.Join(", ", comp.AllowedPresets)}]");
        }

        string[] factions = presetId switch
        {
            "insurgency" => ["govfor", "clf", "scientist"],
            "forceonforce" => ["govfor", "opfor", "scientist"],
            "distresssignal" => ["govfor"],
            _ => ["scientist"], // corporate fallback (e.g. colonyfall)
        };

        foreach (var faction in factions)
        {
            try
            {
                var factionData = master.GetOrCreateFactionData(faction);
                ActivateFactionObjectives(faction, 1,
                    SelectObjectives(faction, modeObjectives, 1,
                        GetRandomObjectiveCount(factionData.MaxMinorObjectives, factionData.MinMinorObjectives)));
                ActivateFactionObjectives(faction, 2,
                    SelectObjectives(faction, modeObjectives, 2,
                        GetRandomObjectiveCount(factionData.MaxMajorObjectives, factionData.MinMajorObjectives)));
            }
            catch (Exception ex)
            {
                _logs.Error($"[OBJ-CTRL] Failed to activate {faction} objectives! {ex}");
            }
        }

        try
        {
            var neutralCandidates = modeObjectives
                .Where(x => x.Comp is { FactionNeutral: true }
                    && (x.Comp.ObjectiveLevel != 3 || x.Comp.RollAnyway))
                .ToList();

            int neutralCap = GetRandomObjectiveCount(master.MaxNeutralObjectives, master.MinNeutralObjectives);
            _logs.Info($"[OBJ-CTRL] Neutral: Found {neutralCandidates.Count} candidates, max allowed = {neutralCap}");

            if (neutralCandidates.Count > neutralCap)
                neutralCandidates = WeightedRandomPick(neutralCandidates, neutralCap);

            foreach (var (uid, obj) in neutralCandidates)
            {
                ActivateObjective(uid, obj);
                _logs.Debug($"[OBJ-CTRL] Activated neutral objective '{obj.ObjectiveDescription}'");
            }
        }
        catch (Exception ex) { _logs.Error($"[OBJ-CTRL] Failed to activate neutral objectives: {ex.Message}!"); }
        master.SelectionComplete = true;
    }

    private void SpawnMissingCatalogObjectives(EntityUid bestPlanetGrid, MapId primaryMapId, string presetId)
    {
        var planetMaps = _zLevels.GetAllNetworkMapIds(primaryMapId);
        var compFactory = EntityManager.ComponentFactory;

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.TryComp<KillObjectiveComponent>(out var killComp, compFactory))
            {
                if (killComp.Catalog)
                    TrySpawnCatalogObjective(proto, presetId, bestPlanetGrid, planetMaps, () => IsKillCatalogFeasible(killComp, planetMaps));
                continue;
            }

            if (proto.TryComp<FetchObjectiveComponent>(out var fetchComp, compFactory))
            {
                if (fetchComp.Catalog)
                    TrySpawnCatalogObjective(proto, presetId, bestPlanetGrid, planetMaps, () => IsFetchCatalogFeasible(fetchComp, planetMaps));
            }
        }
    }

    private void TrySpawnCatalogObjective(
        EntityPrototype proto,
        string presetId,
        EntityUid bestPlanetGrid,
        HashSet<MapId> planetMaps,
        Func<bool> isFeasible)
    {
        var compFactory = EntityManager.ComponentFactory;
        if (!proto.TryComp<CMUObjectiveComponent>(out var objComp, compFactory))
            return;

        var modeMatch = objComp.FactionNeutral
            ? objComp.AllowedPresets.Count == 0 || objComp.AllowedPresets.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase))
            : objComp.AllowedPresets.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        if (!modeMatch)
            return;

        if (_allObjectives.Any(o => o.Comp.Id == objComp.Id && Exists(o.Uid) && planetMaps.Contains(Transform(o.Uid).MapID)))
            return;

        if (!isFeasible())
            return;

        var coords = new EntityCoordinates(bestPlanetGrid, Vector2.Zero);
        if (proto.TryComp<FetchObjectiveComponent>(out var fetchComp, compFactory)
            && !string.IsNullOrEmpty(fetchComp.TargetPrototype)
            && TryGetCatalogTarget(fetchComp.TargetPrototype, planetMaps, out var target))
            coords = Transform(target).Coordinates;

        Spawn(proto.ID, coords);
        _logs.Debug($"[OBJ-CATALOG] Spawned catalog objective '{proto.ID}' ('{objComp.Id}') for preset '{presetId}'.");
    }

    private bool IsFetchCatalogFeasible(FetchObjectiveComponent fetchComp, HashSet<MapId> planetMaps)
    {
        if (fetchComp.UseAnyEntity && !string.IsNullOrEmpty(fetchComp.TargetPrototype) &&
            TryGetCatalogTarget(fetchComp.TargetPrototype, planetMaps, out _))
        {
            return true;
        }

        return CatalogMarkerExists(fetchComp.SpawnMarkerId, planetMaps);
    }

    private bool IsKillCatalogFeasible(KillObjectiveComponent killComp, HashSet<MapId> planetMaps)
    {
        if (!killComp.SpawnMob)
            return true;

        return CatalogMarkerExists(killComp.SpawnMarkerId, planetMaps);
    }

    private bool TryGetCatalogTarget(string targetPrototype, HashSet<MapId> planetMaps, out EntityUid target)
    {
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (planetMaps.Contains(xform.MapID) && meta.EntityPrototype?.ID == targetPrototype)
            {
                target = uid;
                return true;
            }
        }

        target = EntityUid.Invalid;
        return false;
    }

    private bool CatalogMarkerExists(string? spawnMarkerId, HashSet<MapId> planetMaps)
    {
        var query = EntityQueryEnumerator<CMUObjectiveMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var markerUid, out var markerComp, out var xform))
        {
            if (HasComp<CMUObjectiveComponent>(markerUid))
                continue;

            if (!planetMaps.Contains(xform.MapID))
                continue;

            if (!string.IsNullOrEmpty(spawnMarkerId) && markerComp.FetchId == spawnMarkerId)
                return true;

            if (markerComp.Generic)
                return true;
        }

        return false;
    }
}
