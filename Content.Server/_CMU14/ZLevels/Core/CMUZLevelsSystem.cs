using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem : CMUSharedZLevelsSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TransformSystem _transform = default!;

    public CMUZLevelOpeningCache OpeningCache => _zOpeningCache;

    public override void Initialize()
    {
        base.Initialize();
        InitView();
        InitAudio();
        InitTransitionBudget();
        InitializeActivation();

        SubscribeLocalEvent<PostGameMapLoad>(OnGameMapLoad, after: [typeof(StationSystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_zLevelsEnabled)
            return;

        UpdateZMovement(frameTime);
        UpdateView(frameTime);
    }

    private void OnGameMapLoad(PostGameMapLoad ev)
    {
        if (ev.GameMap.MapsAbove.Count == 0 && ev.GameMap.MapsBelow.Count == 0)
            return;

        var stationNetwork = CreateZNetwork();
        _meta.SetEntityName(stationNetwork, $"Station z-Network: {ev.GameMap.MapName}");

        var mainMap = _map.GetMap(ev.Map);
        Dictionary<EntityUid, int> dict = new();
        dict.Add(mainMap, 0);

        var stationsById = new Dictionary<string, EntityUid>(StringComparer.OrdinalIgnoreCase);
        var stations = new HashSet<EntityUid>();
        foreach (var grid in ev.Grids)
        {
            if (_station.GetOwningStation(grid) is not { } station)
                continue;

            stations.Add(station);
            if (TryComp<BecomesStationComponent>(grid, out var becomesStation))
                stationsById[becomesStation.Id] = station;
        }

        EntityManager.AddComponents(mainMap, ev.GameMap.ZLevelsComponentOverrides);

        //Loading maps below first
        var depth = -1;
        foreach (var mapBelow in ev.GameMap.MapsBelow)
        {
            if (!_mapLoader.TryLoadMap(mapBelow, out var mapEnt, out var grids))
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            EntityManager.AddComponents(mapEnt.Value, ev.GameMap.ZLevelsComponentOverrides);
            AddZLevelGridsToStations(grids, stationsById, stations);
            _map.InitializeMap(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"{ev.GameMap.MapName} [{depth}]");
            dict.Add(mapEnt.Value, depth);
            depth--;
        }

        //Loading maps above next
        depth = 1;
        foreach (var mapAbove in ev.GameMap.MapsAbove)
        {
            if (!_mapLoader.TryLoadMap(mapAbove, out var mapEnt, out var grids))
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            EntityManager.AddComponents(mapEnt.Value, ev.GameMap.ZLevelsComponentOverrides);
            AddZLevelGridsToStations(grids, stationsById, stations);
            _map.InitializeMap(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"{ev.GameMap.MapName} [{depth}]");
            dict.Add(mapEnt.Value, depth);
            depth++;
        }

        TryAddMapsIntoZNetwork(stationNetwork, dict);
    }

    private void AddZLevelGridsToStations(
        HashSet<Entity<MapGridComponent>> grids,
        IReadOnlyDictionary<string, EntityUid> stationsById,
        IReadOnlySet<EntityUid> stations)
    {
        foreach (var grid in grids)
        {
            EntityUid? station = null;
            if (TryComp<BecomesStationComponent>(grid, out var becomesStation) &&
                stationsById.TryGetValue(becomesStation.Id, out var matchingStation))
            {
                station = matchingStation;
            }
            else if (grids.Count == 1 && stations.Count == 1)
            {
                station = stations.First();
            }

            if (station is not { } resolvedStation)
            {
                Log.Warning($"Could not associate z-level grid {ToPrettyString(grid)} with a station.");
                continue;
            }

            _station.AddGridToStation(resolvedStation, grid);
        }
    }

}
