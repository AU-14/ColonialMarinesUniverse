using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem : CMUSharedZLevelsSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    public CMUZLevelOpeningCache OpeningCache => _zOpeningCache;

    public override void Initialize()
    {
        base.Initialize();
        InitView();
        InitAudio();
        InitTransitionBudget();
        InitializeActivation();

        SubscribeLocalEvent<PostGameMapLoad>(OnGameMapLoad);
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

        EntityManager.AddComponents(mainMap, ev.GameMap.ZLevelsComponentOverrides);

        //Loading maps below first
        var depth = ev.GameMap.MapsBelow.Count * -1;
        foreach (var mapBelow in ev.GameMap.MapsBelow)
        {
            if (!_mapLoader.TryLoadMap(mapBelow, out var mapEnt, out _))
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            EntityManager.AddComponents(mapEnt.Value, ev.GameMap.ZLevelsComponentOverrides);
            _map.InitializeMap(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"{ev.GameMap.MapName} [{depth}]");
            dict.Add(mapEnt.Value, depth);
            depth++;
        }

        //Loading maps above next
        depth = 1;
        foreach (var mapAbove in ev.GameMap.MapsAbove)
        {
            if (!_mapLoader.TryLoadMap(mapAbove, out var mapEnt, out _))
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            EntityManager.AddComponents(mapEnt.Value, ev.GameMap.ZLevelsComponentOverrides);
            _map.InitializeMap(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"{ev.GameMap.MapName} [{depth}]");
            dict.Add(mapEnt.Value, depth);
            depth++;
        }

        if (TryAddMapsIntoZNetwork(stationNetwork, dict))
            StabilizeZLevelDeckGrids(dict.Keys);
    }

    private void StabilizeZLevelDeckGrids(IEnumerable<EntityUid> maps)
    {
        foreach (var mapUid in maps)
        {
            if (!TryComp<MapComponent>(mapUid, out var map))
                continue;

            var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (query.MoveNext(out var gridUid, out _, out var gridXform))
            {
                if (gridXform.MapID != map.MapId)
                    continue;

                _shuttle.Disable(gridUid);

                if (TryComp<ShuttleComponent>(gridUid, out var shuttle))
                    shuttle.Enabled = false;
            }
        }
    }
}
