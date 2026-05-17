using Content.Server.GameTicking;
using Content.Shared._CMU14.Underground;
using Content.Shared._CMU14.Underground.BurrowerTunnel;
using Content.Shared._CMU14.Underground.Markers;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Burrow;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Burial.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Underground;

public sealed class UndergroundMapSystem : SharedUndergroundMapSystem
{
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly SharedRMCExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    private readonly Queue<EntityUid> _pendingGrids = new();

    private EntityQuery<UndergroundDugMarkerComponent> _dugMarkerQuery;
    private EntityQuery<UndergroundEntranceComponent> _entranceQuery;
    private EntityQuery<UndergroundMapComponent> _undergroundQuery;
    private EntityQuery<UndergroundRockComponent> _rockQuery;
    private EntityQuery<UndergroundSupportBeamComponent> _beamQuery;
    private EntityQuery<UndergroundSurfaceMapComponent> _surfaceQuery;
    private EntityQuery<XenoComponent> _xenoQuery;

    // Cave-in settings
    private TimeSpan _nextCaveInCheck;
    private static readonly TimeSpan CaveInCheckInterval = TimeSpan.FromSeconds(10);
    private const float CaveInChance = 0.05f;

    // 8-directional adjacency offsets
    private static readonly Vector2i[] AdjacentOffsets =
    {
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0),              new(1, 0),
        new(-1, 1),  new(0, 1),  new(1, 1),
    };

    public override void Initialize()
    {
        base.Initialize();

        _beamQuery = GetEntityQuery<UndergroundSupportBeamComponent>();
        _dugMarkerQuery = GetEntityQuery<UndergroundDugMarkerComponent>();
        _entranceQuery = GetEntityQuery<UndergroundEntranceComponent>();
        _undergroundQuery = GetEntityQuery<UndergroundMapComponent>();
        _rockQuery = GetEntityQuery<UndergroundRockComponent>();
        _surfaceQuery = GetEntityQuery<UndergroundSurfaceMapComponent>();
        _xenoQuery = GetEntityQuery<XenoComponent>();

        // PostGameMapLoad fires for all maps loaded via GameTicker.LoadGameMap(),
        // which covers every CMU game mode (DistressSignal, ColonyFall, Insurgency, ForceOnForce).
        // Grids are queued and processed in Update() to give tiles one tick to commit.
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        // Shovel digging (activate item in hand)
        SubscribeLocalEvent<ShovelComponent, UseInHandEvent>(OnShovelUseInHand);
        SubscribeLocalEvent<ShovelComponent, UndergroundDigDoAfterEvent>(OnDigDoAfter);

        // Entrance usage
        SubscribeLocalEvent<UndergroundEntranceComponent, ActivateInWorldEvent>(OnEntranceActivate);
        SubscribeLocalEvent<UndergroundEntranceComponent, UndergroundEntranceDoAfterEvent>(OnEntranceDoAfter);

        // Entrance destruction (shovel fill and C4/explosive delete)
        SubscribeLocalEvent<UndergroundEntranceComponent, InteractUsingEvent>(OnEntranceInteractUsing);
        SubscribeLocalEvent<UndergroundEntranceComponent, UndergroundFillEntranceDoAfterEvent>(OnEntranceFillDoAfter);
        SubscribeLocalEvent<UndergroundEntranceComponent, EntityTerminatingEvent>(OnEntranceTerminating);

        // Entrance peek ("Look Through" verb)
        SubscribeLocalEvent<UndergroundEntranceComponent, GetVerbsEvent<AlternativeVerb>>(OnEntranceGetAltVerbs);
        SubscribeLocalEvent<UndergroundWatchingComponent, MoveInputEvent>(OnWatchingMoveInput);
        SubscribeLocalEvent<UndergroundWatchingComponent, ComponentRemove>(OnWatchingRemove);
        SubscribeLocalEvent<UndergroundWatchingComponent, EntityTerminatingEvent>(OnWatchingTerminating);

        // Support beam placement restriction
        SubscribeLocalEvent<UndergroundSupportBeamComponent, AnchorAttemptEvent>(OnBeamAnchorAttempt);

        // Burrower tunnel menu
        SubscribeLocalEvent<BurrowerTunnelChoiceComponent, BurrowerChooseTunnelActionEvent>(OnBurrowerChooseTunnel);
        SubscribeLocalEvent<BurrowerTunnelChoiceComponent, BurrowerTunnelChosenBuiMsg>(OnBurrowerTunnelChosen);

        // Xeno burrower digging (underground entrance)
        SubscribeLocalEvent<XenoComponent, XenoDigUndergroundActionEvent>(OnXenoDigUnderground);
        SubscribeLocalEvent<XenoComponent, XenoDigUndergroundDoAfterEvent>(OnXenoDigDoAfter);

        // Burrower deals 1.5x melee damage to underground rocks
        SubscribeLocalEvent<XenoBurrowComponent, MeleeHitEvent>(OnBurrowerMeleeHit);

        // Rock destruction (Destructible path: mining, explosions that deal damage)
        SubscribeLocalEvent<UndergroundRockComponent, DestructionEventArgs>(OnRockDestroyed);

        // Rock deletion (direct delete path: C4's RMCExplosiveDelete bypasses Destructible)
        SubscribeLocalEvent<UndergroundRockComponent, EntityTerminatingEvent>(OnRockTerminating);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _pendingGrids.Clear();
        _nextCaveInCheck = TimeSpan.Zero;
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        foreach (var gridUid in ev.Grids)
        {
            if (HasComp<MapGridComponent>(gridUid))
            {
                Log.Info($"UndergroundMapSystem: PostGameMapLoad queuing grid {gridUid}");
                _pendingGrids.Enqueue(gridUid);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_pendingGrids.TryDequeue(out var gridUid))
        {
            if (TerminatingOrDeleted(gridUid))
                continue;

            if (!TryComp(gridUid, out MapGridComponent? grid))
                continue;

            Log.Info($"UndergroundMapSystem: creating underground for grid {gridUid}, AABB={grid.LocalAABB}");
            CreateUndergroundMap(gridUid);
        }

        // Cave-in check: every 10 seconds, unsupported dug tiles have a chance to collapse.
        var curTime = _timing.CurTime;
        if (curTime < _nextCaveInCheck)
            return;

        _nextCaveInCheck = curTime + CaveInCheckInterval;

        var caveInList = new List<(EntityUid Grid, MapGridComponent GridComp, EntityUid Marker, Vector2i Tile)>();
        var markerQuery = EntityQueryEnumerator<UndergroundDugMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out _, out var xform))
        {
            if (_transform.GetGrid(xform.Coordinates) is not { } markerGridUid)
                continue;

            // Only check markers on underground grids
            if (!_undergroundQuery.HasComp(markerGridUid))
                continue;

            if (!TryComp(markerGridUid, out MapGridComponent? markerGrid))
                continue;

            var tileIndices = _mapSystem.CoordinatesToTile(markerGridUid, markerGrid, xform.Coordinates);

            if (!IsTileSupported(markerGridUid, markerGrid, tileIndices) && _random.Prob(CaveInChance))
            {
                caveInList.Add((markerGridUid, markerGrid, markerUid, tileIndices));
            }
        }

        // Process cave-ins (separate loop to avoid modifying while enumerating)
        foreach (var (gridUid, grid, markerUid, tileIndices) in caveInList)
        {
            if (TerminatingOrDeleted(markerUid))
                continue;

            // Delete the dug marker
            QueueDel(markerUid);

            // Spawn a rock to seal the tile
            var coords = _mapSystem.GridTileToLocal(gridUid, grid, tileIndices);
            Spawn("CMUUndergroundRock", coords);

            // Damage entities on this tile
            var caveInDamage = new DamageSpecifier();
            caveInDamage.DamageDict.Add("Blunt", 30);

            var lookup = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
            while (lookup.MoveNext(out var entOnTile))
            {
                if (HasComp<DamageableComponent>(entOnTile))
                    _damageable.TryChangeDamage(entOnTile, caveInDamage);
            }

            // Also damage non-anchored entities at this position
            var worldPos = _transform.ToMapCoordinates(coords);
            foreach (var ent in _entityLookup.GetEntitiesInRange(worldPos, 0.5f))
            {
                if (HasComp<DamageableComponent>(ent))
                    _damageable.TryChangeDamage(ent, caveInDamage);
            }

            _popup.PopupCoordinates(Loc.GetString("cmu-underground-cave-in"), coords, PopupType.LargeCaution);
        }

        // Suffocation: entities in sealed caverns (no reachable entrance) take damage
        CheckUndergroundSuffocation();
    }

    /// <summary>
    /// Creates the underground mirror map for a given surface grid.
    /// Idempotent: does nothing if the grid already has an underground map.
    /// </summary>
    public void CreateUndergroundMap(EntityUid surfaceGridUid)
    {
        // Idempotent: skip if already set up
        if (_surfaceQuery.HasComp(surfaceGridUid))
            return;

        if (!TryComp(surfaceGridUid, out MapGridComponent? surfaceGrid))
        {
            Log.Error("UndergroundMapSystem: surface grid has no MapGridComponent.");
            return;
        }

        // Look up the dirt tile definition
        if (!_tileDef.TryGetDefinition("CMUFloorUndergroundDirt", out var dirtTileDef))
        {
            Log.Error("UndergroundMapSystem: CMUFloorUndergroundDirt tile definition not found.");
            return;
        }

        var dirtTile = new Tile(dirtTileDef.TileId);

        // Enumerate actual tiles on the surface grid instead of using LocalAABB
        // (LocalAABB may not be recalculated yet at map load time).
        var tiles = new List<(Vector2i Index, Tile Tile)>();
        foreach (var tileRef in _mapSystem.GetAllTiles(surfaceGridUid, surfaceGrid))
        {
            tiles.Add((tileRef.GridIndices, dirtTile));
        }

        if (tiles.Count == 0)
        {
            Log.Warning($"UndergroundMapSystem: surface grid {surfaceGridUid} has no tiles, skipping underground creation.");
            return;
        }

        // Create a new map for the underground
        var undergroundMapUid = _mapSystem.CreateMap(out var undergroundMapId, runMapInit: false);

        // Add a grid component so we can place tiles
        var undergroundGrid = EnsureComp<MapGridComponent>(undergroundMapUid);

        _mapSystem.SetTiles(undergroundMapUid, undergroundGrid, tiles);

        // Spawn indestructible border rocks on every edge tile.
        // An edge tile is any tile that has at least one empty (non-existent) neighbor.
        var tileSet = new HashSet<Vector2i>(tiles.Count);
        foreach (var (index, _) in tiles)
        {
            tileSet.Add(index);
        }

        foreach (var index in tileSet)
        {
            var isEdge = false;
            foreach (var offset in AdjacentOffsets)
            {
                if (!tileSet.Contains(index + offset))
                {
                    isEdge = true;
                    break;
                }
            }

            if (isEdge)
            {
                var coords = _mapSystem.GridTileToLocal(undergroundMapUid, undergroundGrid, index);
                Spawn("CMUUndergroundBorderRock", coords);
            }
        }

        // Link the two maps together
        var undergroundComp = EnsureComp<UndergroundMapComponent>(undergroundMapUid);
        undergroundComp.SurfaceGrid = surfaceGridUid;
        Dirty(undergroundMapUid, undergroundComp);

        var surfaceComp = EnsureComp<UndergroundSurfaceMapComponent>(surfaceGridUid);
        surfaceComp.UndergroundGrid = undergroundMapUid;
        Dirty(surfaceGridUid, surfaceComp);

        // Process mapper-placed markers on the surface grid
        ProcessUndergroundMarkers(surfaceGridUid, surfaceGrid, undergroundMapUid, undergroundGrid);

        // Initialize the underground map now that tiles are placed
        _mapSystem.InitializeMap(undergroundMapUid);

        Log.Info($"UndergroundMapSystem: created underground mirror map {undergroundMapId} " +
                 $"with {tiles.Count} tiles for surface grid {surfaceGridUid}.");
    }

    private void ProcessUndergroundMarkers(
        EntityUid surfaceGridUid,
        MapGridComponent surfaceGrid,
        EntityUid undergroundGridUid,
        MapGridComponent undergroundGrid)
    {
        // Scan all anchored entities on the surface grid for marker components
        var markersProcessed = 0;

        foreach (var tileRef in _mapSystem.GetAllTiles(surfaceGridUid, surfaceGrid))
        {
            var tileIndices = tileRef.GridIndices;
            var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(surfaceGridUid, surfaceGrid, tileIndices);

            while (anchored.MoveNext(out var markerUid))
            {
                var ugCoords = _mapSystem.GridTileToLocal(undergroundGridUid, undergroundGrid, tileIndices);

                if (HasComp<UndergroundEntranceMarkerComponent>(markerUid))
                {
                    // Spawn paired entrance
                    var surfCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGrid, tileIndices);
                    var surfEntrance = Spawn("CMUUndergroundEntranceSurface", surfCoords);
                    var ugEntrance = Spawn("CMUUndergroundEntranceBelow", ugCoords);
                    LinkEntrances(surfEntrance, ugEntrance);
                    SpawnDugMarker(undergroundGridUid, undergroundGrid, tileIndices);
                    QueueDel(markerUid.Value);
                    markersProcessed++;
                }
                else if (HasComp<UndergroundPreDigMarkerComponent>(markerUid))
                {
                    // Pre-dig: place dug marker (no rock will spawn here)
                    SpawnDugMarker(undergroundGridUid, undergroundGrid, tileIndices);
                    QueueDel(markerUid.Value);
                    markersProcessed++;
                }
                else if (HasComp<UndergroundIndestructibleRockMarkerComponent>(markerUid))
                {
                    // Place indestructible border rock
                    Spawn("CMUUndergroundBorderRock", ugCoords);
                    QueueDel(markerUid.Value);
                    markersProcessed++;
                }
                else if (TryComp(markerUid, out UndergroundLootMarkerComponent? lootMarker))
                {
                    // Pre-dig and spawn loot
                    SpawnDugMarker(undergroundGridUid, undergroundGrid, tileIndices);
                    Spawn(lootMarker.Spawn, ugCoords);
                    QueueDel(markerUid.Value);
                    markersProcessed++;
                }
            }
        }

        if (markersProcessed > 0)
            Log.Info($"UndergroundMapSystem: processed {markersProcessed} mapper markers for grid {surfaceGridUid}.");
    }

    #region Support Beam Placement

    private void OnBeamAnchorAttempt(Entity<UndergroundSupportBeamComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var coordinates = ent.Owner.ToCoordinates();
        if (_transform.GetGrid(coordinates) is not { } gridUid || !_undergroundQuery.HasComp(gridUid))
        {
            if (args.User is { } user)
                _popup.PopupEntity(Loc.GetString("cmu-underground-beam-surface"), user, user, PopupType.SmallCaution);

            args.Cancel();
        }
    }

    #endregion

    #region Shovel Digging

    private void OnShovelUseInHand(Entity<ShovelComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Humans only
        if (_xenoQuery.HasComp(args.User))
            return;

        // Use the player's current position as the dig location
        var coordinates = _transform.GetMoverCoordinates(args.User);

        // Determine if we are on the surface or underground
        if (_transform.GetGrid(coordinates) is not { } gridUid)
            return;

        bool diggingUp;
        if (_surfaceQuery.HasComp(gridUid))
        {
            diggingUp = false;
        }
        else if (_undergroundQuery.HasComp(gridUid))
        {
            diggingUp = true;
        }
        else
        {
            // Not on a grid with underground support
            return;
        }

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        // Check tile is diggable
        var tileRef = _mapSystem.GetTileRef(gridUid, grid, coordinates);
        var tileDef = (ContentTileDefinition) _tileDef[tileRef.Tile.TypeId];
        if (!tileDef.CanDig)
            return;

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, grid, coordinates);

        // Resolve surface grid and coordinates for area and ceiling checks.
        // When underground the area data lives on the paired surface grid.
        EntityUid surfaceGridUid;
        MapGridComponent surfaceGridComp;
        if (diggingUp)
        {
            if (!_undergroundQuery.TryComp(gridUid, out var ugComp) ||
                !TryComp(ugComp.SurfaceGrid, out MapGridComponent? sg))
                return;

            surfaceGridUid = ugComp.SurfaceGrid;
            surfaceGridComp = sg;
        }
        else
        {
            surfaceGridUid = gridUid;
            surfaceGridComp = grid;
        }

        var surfaceCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGridComp, tileIndices);

        // When digging up, the corresponding surface tile must be clear of walls and structures
        if (diggingUp)
        {
            var surfAnchored = _mapSystem.GetAnchoredEntitiesEnumerator(surfaceGridUid, surfaceGridComp, tileIndices);
            if (surfAnchored.MoveNext(out _))
            {
                _popup.PopupEntity(Loc.GetString("cmu-underground-surface-blocked"), args.User, args.User, PopupType.SmallCaution);
                return;
            }
        }

        // Check area allows underground
        if (_area.TryGetArea(surfaceCoords, out var area, out _) &&
            area.Value.Comp.NoUnderground)
        {
            _popup.PopupEntity(Loc.GetString("cmu-underground-area-blocked"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        // Block digging in lightly roofed areas (ceiling level 1 or 2)
        var ceiling = GetAreaCeilingLevel(surfaceCoords);
        if (ceiling is 1 or 2)
        {
            _popup.PopupEntity(Loc.GetString("cmu-underground-ceiling-blocked"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        // Check no entrance already at this tile
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
        while (anchored.MoveNext(out var anchoredUid))
        {
            if (_entranceQuery.HasComp(anchoredUid))
            {
                _popup.PopupEntity(Loc.GetString("cmu-underground-already-entrance"), args.User, args.User, PopupType.SmallCaution);
                return;
            }
        }

        var tileCenter = _mapSystem.GridTileToLocal(gridUid, grid, tileIndices);
        var ev = new UndergroundDigDoAfterEvent(GetNetCoordinates(tileCenter), diggingUp);
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(30), ev, ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var msg = diggingUp
                ? Loc.GetString("cmu-underground-start-digging-up")
                : Loc.GetString("cmu-underground-start-digging-down");
            _popup.PopupEntity(msg, args.User, args.User);
        }

        args.Handled = true;
    }

    private void OnDigDoAfter(Entity<ShovelComponent> ent, ref UndergroundDigDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var coordinates = GetCoordinates(args.Coordinates);
        if (_transform.GetGrid(coordinates) is not { } gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, grid, coordinates);

        if (args.DiggingUp)
            DigUp(gridUid, grid, tileIndices);
        else
            DigDown(gridUid, grid, tileIndices);
    }

    private void DigDown(EntityUid surfaceGridUid, MapGridComponent surfaceGrid, Vector2i tileIndices)
    {
        if (!_surfaceQuery.TryComp(surfaceGridUid, out var surfaceComp) ||
            surfaceComp.UndergroundGrid is not { } undergroundUid ||
            !TryComp(undergroundUid, out MapGridComponent? undergroundGrid))
        {
            return;
        }

        // Spawn surface entrance
        var surfaceCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGrid, tileIndices);
        var surfaceEntrance = Spawn("CMUUndergroundEntranceSurface", surfaceCoords);

        // Spawn underground entrance at matching coordinates
        var undergroundCoords = _mapSystem.GridTileToLocal(undergroundUid, undergroundGrid, tileIndices);
        var undergroundEntrance = Spawn("CMUUndergroundEntranceBelow", undergroundCoords);

        // Link the entrances
        LinkEntrances(surfaceEntrance, undergroundEntrance);

        // Place a dug marker at the underground entrance tile so no rock spawns there
        SpawnDugMarker(undergroundUid, undergroundGrid, tileIndices);
    }

    private void DigUp(EntityUid undergroundGridUid, MapGridComponent undergroundGrid, Vector2i tileIndices)
    {
        if (!_undergroundQuery.TryComp(undergroundGridUid, out var undergroundComp) ||
            !TryComp(undergroundComp.SurfaceGrid, out MapGridComponent? surfaceGrid))
        {
            return;
        }

        var surfaceGridUid = undergroundComp.SurfaceGrid;

        // Spawn underground entrance
        var undergroundCoords = _mapSystem.GridTileToLocal(undergroundGridUid, undergroundGrid, tileIndices);
        var undergroundEntrance = Spawn("CMUUndergroundEntranceBelow", undergroundCoords);

        // Spawn surface entrance at matching coordinates
        var surfaceCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGrid, tileIndices);
        var surfaceEntrance = Spawn("CMUUndergroundEntranceSurface", surfaceCoords);

        // Link the entrances
        LinkEntrances(surfaceEntrance, undergroundEntrance);

        // Place a dug marker at the underground entrance tile
        SpawnDugMarker(undergroundGridUid, undergroundGrid, tileIndices);
    }

    private void LinkEntrances(EntityUid surfaceEntrance, EntityUid undergroundEntrance)
    {
        if (TryComp(surfaceEntrance, out UndergroundEntranceComponent? surfComp))
        {
            surfComp.Other = undergroundEntrance;
            Dirty(surfaceEntrance, surfComp);
        }

        if (TryComp(undergroundEntrance, out UndergroundEntranceComponent? ugComp))
        {
            ugComp.Other = surfaceEntrance;
            Dirty(undergroundEntrance, ugComp);
        }
    }

    #endregion

    #region Burrower Tunnel Menu

    private void OnBurrowerChooseTunnel(Entity<BurrowerTunnelChoiceComponent> ent, ref BurrowerChooseTunnelActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var cooldownRemaining = TimeSpan.Zero;
        var curTime = _timing.CurTime;
        if (ent.Comp.NextHiveTunnelAt > curTime)
            cooldownRemaining = ent.Comp.NextHiveTunnelAt - curTime;

        _ui.SetUiState(ent.Owner, BurrowerTunnelUI.Key, new BurrowerTunnelBuiState(ent.Comp.Choice, cooldownRemaining));
        _ui.TryOpenUi(ent.Owner, BurrowerTunnelUI.Key, ent.Owner);
    }

    private void OnBurrowerTunnelChosen(Entity<BurrowerTunnelChoiceComponent> ent, ref BurrowerTunnelChosenBuiMsg args)
    {
        ent.Comp.Choice = args.Choice;
        Dirty(ent);

        switch (args.Choice)
        {
            case BurrowerTunnelType.HiveTunnel:
            {
                var curTime = _timing.CurTime;
                if (ent.Comp.NextHiveTunnelAt > curTime)
                {
                    var remaining = (int)(ent.Comp.NextHiveTunnelAt - curTime).TotalSeconds;
                    _popup.PopupEntity(Loc.GetString("cmu-underground-tunnel-on-cooldown", ("seconds", remaining)), ent.Owner, ent.Owner, PopupType.SmallCaution);
                    return;
                }

                // Raise the hive tunnel event; XenoTunnelSystem handles the rest
                var ev = new XenoDigTunnelActionEvent { Performer = ent.Owner };
                RaiseLocalEvent(ent.Owner, (object) ev, broadcast: true);

                if (ev.Handled)
                {
                    ent.Comp.NextHiveTunnelAt = curTime + TimeSpan.FromSeconds(300);
                    Dirty(ent);
                }

                break;
            }
            case BurrowerTunnelType.UndergroundEntrance:
            {
                // Raise the underground entrance event; our own handler processes it
                var ev = new XenoDigUndergroundActionEvent { Performer = ent.Owner };
                RaiseLocalEvent(ent.Owner, (object) ev, broadcast: true);
                break;
            }
        }
    }

    #endregion

    #region Xeno Burrower Digging

    private void OnXenoDigUnderground(Entity<XenoComponent> ent, ref XenoDigUndergroundActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xenoPlasma.HasPlasmaPopup(ent.Owner, args.PlasmaCost))
            return;

        var coordinates = _transform.GetMoverCoordinates(ent.Owner);

        if (_transform.GetGrid(coordinates) is not { } gridUid)
            return;

        bool diggingUp;
        if (_surfaceQuery.HasComp(gridUid))
            diggingUp = false;
        else if (_undergroundQuery.HasComp(gridUid))
            diggingUp = true;
        else
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var tileRef = _mapSystem.GetTileRef(gridUid, grid, coordinates);
        var tileDef = (ContentTileDefinition) _tileDef[tileRef.Tile.TypeId];
        if (!tileDef.CanDig)
            return;

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, grid, coordinates);

        // Resolve surface grid for area and ceiling checks
        EntityUid surfaceGridUid;
        MapGridComponent surfaceGridComp;
        if (diggingUp)
        {
            if (!_undergroundQuery.TryComp(gridUid, out var ugComp) ||
                !TryComp(ugComp.SurfaceGrid, out MapGridComponent? sg))
                return;

            surfaceGridUid = ugComp.SurfaceGrid;
            surfaceGridComp = sg;
        }
        else
        {
            surfaceGridUid = gridUid;
            surfaceGridComp = grid;
        }

        var surfaceCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGridComp, tileIndices);

        // When digging up, surface tile must be clear
        if (diggingUp)
        {
            var surfAnchored = _mapSystem.GetAnchoredEntitiesEnumerator(surfaceGridUid, surfaceGridComp, tileIndices);
            if (surfAnchored.MoveNext(out _))
            {
                _popup.PopupEntity(Loc.GetString("cmu-underground-surface-blocked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
                return;
            }
        }

        // Check area allows underground
        if (_area.TryGetArea(surfaceCoords, out var area, out _) &&
            area.Value.Comp.NoUnderground)
        {
            _popup.PopupEntity(Loc.GetString("cmu-underground-area-blocked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        // Block digging in lightly roofed areas (ceiling level 1 or 2)
        var ceiling = GetAreaCeilingLevel(surfaceCoords);
        if (ceiling is 1 or 2)
        {
            _popup.PopupEntity(Loc.GetString("cmu-underground-ceiling-blocked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        // Check no entrance already at this tile
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
        while (anchored.MoveNext(out var anchoredUid))
        {
            if (_entranceQuery.HasComp(anchoredUid))
            {
                _popup.PopupEntity(Loc.GetString("cmu-underground-already-entrance"), ent.Owner, ent.Owner, PopupType.SmallCaution);
                return;
            }
        }

        var tileCenter = _mapSystem.GridTileToLocal(gridUid, grid, tileIndices);
        var ev = new XenoDigUndergroundDoAfterEvent(GetNetCoordinates(tileCenter), diggingUp);
        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, TimeSpan.FromSeconds(15), ev, ent, used: ent)
        {
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var msg = diggingUp
                ? Loc.GetString("cmu-underground-xeno-start-digging-up")
                : Loc.GetString("cmu-underground-xeno-start-digging-down");
            _popup.PopupEntity(msg, ent.Owner, ent.Owner);
        }

        args.Handled = true;
    }

    private void OnXenoDigDoAfter(Entity<XenoComponent> ent, ref XenoDigUndergroundDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!_xenoPlasma.TryRemovePlasma(ent.Owner, 200))
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        if (_transform.GetGrid(coordinates) is not { } gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, grid, coordinates);

        if (args.DiggingUp)
            XenoDigUp(gridUid, grid, tileIndices, ent.Owner);
        else
            XenoDigDown(gridUid, grid, tileIndices, ent.Owner);
    }

    private void XenoDigDown(EntityUid surfaceGridUid, MapGridComponent surfaceGrid, Vector2i tileIndices, EntityUid xeno)
    {
        if (!_surfaceQuery.TryComp(surfaceGridUid, out var surfaceComp) ||
            surfaceComp.UndergroundGrid is not { } undergroundUid ||
            !TryComp(undergroundUid, out MapGridComponent? undergroundGrid))
        {
            return;
        }

        var surfaceCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGrid, tileIndices);
        var surfaceEntrance = Spawn("CMUXenoUndergroundEntranceSurface", surfaceCoords);

        var undergroundCoords = _mapSystem.GridTileToLocal(undergroundUid, undergroundGrid, tileIndices);
        var undergroundEntrance = Spawn("CMUXenoUndergroundEntranceBelow", undergroundCoords);

        LinkEntrances(surfaceEntrance, undergroundEntrance);
        SpawnDugMarker(undergroundUid, undergroundGrid, tileIndices);

        _hive.SetSameHive(xeno, surfaceEntrance);
        _hive.SetSameHive(xeno, undergroundEntrance);
    }

    private void XenoDigUp(EntityUid undergroundGridUid, MapGridComponent undergroundGrid, Vector2i tileIndices, EntityUid xeno)
    {
        if (!_undergroundQuery.TryComp(undergroundGridUid, out var undergroundComp) ||
            !TryComp(undergroundComp.SurfaceGrid, out MapGridComponent? surfaceGrid))
        {
            return;
        }

        var surfaceGridUid = undergroundComp.SurfaceGrid;

        var undergroundCoords = _mapSystem.GridTileToLocal(undergroundGridUid, undergroundGrid, tileIndices);
        var undergroundEntrance = Spawn("CMUXenoUndergroundEntranceBelow", undergroundCoords);

        var surfaceCoords = _mapSystem.GridTileToLocal(surfaceGridUid, surfaceGrid, tileIndices);
        var surfaceEntrance = Spawn("CMUXenoUndergroundEntranceSurface", surfaceCoords);

        LinkEntrances(surfaceEntrance, undergroundEntrance);
        SpawnDugMarker(undergroundGridUid, undergroundGrid, tileIndices);

        _hive.SetSameHive(xeno, surfaceEntrance);
        _hive.SetSameHive(xeno, undergroundEntrance);
    }

    #endregion

    #region Entrance Usage

    private void OnEntranceActivate(Entity<UndergroundEntranceComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Other is not { } other || TerminatingOrDeleted(other))
        {
            _popup.PopupEntity(Loc.GetString("cmu-underground-leads-nowhere"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var ev = new UndergroundEntranceDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.UseDelay, ev, ent, ent)
        {
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("cmu-underground-start-climbing"), args.User, args.User);
        }

        args.Handled = true;
    }

    private void OnEntranceDoAfter(Entity<UndergroundEntranceComponent> ent, ref UndergroundEntranceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Other is not { } other || TerminatingOrDeleted(other))
            return;

        var coordinates = _transform.GetMapCoordinates(other);
        if (coordinates.MapId == MapId.Nullspace)
            return;

        _transform.SetMapCoordinates(args.User, coordinates);

        // If the player arrived underground, spawn rocks around them
        if (_entranceQuery.TryComp(other, out var otherEntrance) && otherEntrance.IsUnderground)
        {
            var otherCoords = other.ToCoordinates();
            if (_transform.GetGrid(otherCoords) is { } gridUid &&
                TryComp(gridUid, out MapGridComponent? grid))
            {
                var tileIndices = _mapSystem.CoordinatesToTile(gridUid, grid, otherCoords);
                SpawnRocksAround(gridUid, grid, tileIndices);
            }
        }

        _popup.PopupEntity(Loc.GetString("cmu-underground-finish-climbing"), args.User, args.User);
    }

    private void OnEntranceInteractUsing(Entity<UndergroundEntranceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(args.Used, out XenoTunnelFillerComponent? filler))
            return;

        // E-tool must be deployed (toggled on)
        if (TryComp(args.Used, out ItemToggleComponent? toggle) && !toggle.Activated)
            return;

        args.Handled = true;

        var ev = new UndergroundFillEntranceDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(60), ev, ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-underground-start-filling"), args.User, args.User);
    }

    private void OnEntranceFillDoAfter(Entity<UndergroundEntranceComponent> ent, ref UndergroundFillEntranceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        DestroyEntrancePair(ent);
    }

    private void OnEntranceTerminating(Entity<UndergroundEntranceComponent> ent, ref EntityTerminatingEvent args)
    {
        // When one entrance is deleted (C4, admin, etc.), destroy the paired entrance too
        if (ent.Comp.Other is { } other && !TerminatingOrDeleted(other))
        {
            // Trigger matching explosion on the other side
            var otherCoords = _transform.GetMapCoordinates(other);
            _explosion.QueueExplosion(otherCoords, "RMC", 350, 6, 20, null, canCreateVacuum: false);

            // Clear the back-reference to prevent infinite recursion
            if (TryComp(other, out UndergroundEntranceComponent? otherComp))
                otherComp.Other = null;

            QueueDel(other);
        }
    }

    private void DestroyEntrancePair(Entity<UndergroundEntranceComponent> ent)
    {
        var other = ent.Comp.Other;

        // Clear back-references before deleting to prevent recursion
        if (other != null && TryComp(other, out UndergroundEntranceComponent? otherComp))
            otherComp.Other = null;

        ent.Comp.Other = null;

        _popup.PopupCoordinates(Loc.GetString("cmu-underground-entrance-collapsed"), ent.Owner.ToCoordinates(), PopupType.Medium);

        QueueDel(ent);

        if (other != null && !TerminatingOrDeleted(other.Value))
        {
            _popup.PopupCoordinates(Loc.GetString("cmu-underground-entrance-collapsed"), other.Value.ToCoordinates(), PopupType.Medium);
            QueueDel(other.Value);
        }
    }

    #endregion

    #region Entrance Peek

    private void OnEntranceGetAltVerbs(Entity<UndergroundEntranceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (ent.Comp.Other is not { } other || TerminatingOrDeleted(other))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Priority = 100,
            Act = () =>
            {
                if (ent.Comp.Other is not { } target || TerminatingOrDeleted(target))
                    return;

                if (!TryComp(user, out ActorComponent? actor) ||
                    !TryComp(user, out EyeComponent? eye))
                    return;

                EntranceWatch(user, target, actor.PlayerSession);
            },
            Text = Loc.GetString("cmu-underground-look-through"),
        });
    }

    private void EntranceWatch(EntityUid watcher, EntityUid target, ICommonSession session)
    {
        // Clear any existing peek first
        EntranceUnwatch(watcher, session);

        _eye.SetTarget(watcher, target);
        _viewSubscriber.AddViewSubscriber(target, session);

        var watching = EnsureComp<UndergroundWatchingComponent>(watcher);
        watching.Watching = target;
        Dirty(watcher, watching);
    }

    private void EntranceUnwatch(EntityUid watcher, ICommonSession session)
    {
        if (!TryComp(watcher, out EyeComponent? eye))
            return;

        var oldTarget = eye.Target;

        _eye.SetTarget(watcher, null);

        if (oldTarget != null && oldTarget != watcher)
            _viewSubscriber.RemoveViewSubscriber(oldTarget.Value, session);

        RemCompDeferred<UndergroundWatchingComponent>(watcher);
    }

    private void OnWatchingMoveInput(Entity<UndergroundWatchingComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (TryComp(ent, out ActorComponent? actor))
            EntranceUnwatch(ent.Owner, actor.PlayerSession);
    }

    private void OnWatchingRemove(Entity<UndergroundWatchingComponent> ent, ref ComponentRemove args)
    {
        if (TryComp(ent, out ActorComponent? actor))
            EntranceUnwatch(ent.Owner, actor.PlayerSession);
    }

    private void OnWatchingTerminating(Entity<UndergroundWatchingComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryComp(ent, out ActorComponent? actor))
            EntranceUnwatch(ent.Owner, actor.PlayerSession);
    }

    #endregion

    #region Rock Generation and Mining

    private void OnBurrowerMeleeHit(Entity<XenoBurrowComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (!_rockQuery.HasComp(hit))
                continue;

            // Apply 50% bonus damage (total 1.5x) to underground rocks
            var bonus = args.BaseDamage * 0.5f;
            _damageable.TryChangeDamage(hit, bonus);
        }
    }

    private void SpawnRocksAround(EntityUid gridUid, MapGridComponent grid, Vector2i center)
    {
        foreach (var offset in AdjacentOffsets)
        {
            var adjacent = center + offset;
            if (HasDugMarker(gridUid, grid, adjacent))
                continue;

            if (HasRock(gridUid, grid, adjacent))
                continue;

            // Check that a tile exists at this position
            var tileRef = _mapSystem.GetTileRef(gridUid, grid, adjacent);
            if (tileRef.Tile.IsEmpty)
                continue;

            var coords = _mapSystem.GridTileToLocal(gridUid, grid, adjacent);
            Spawn("CMUUndergroundRock", coords);
        }
    }

    private void OnRockDestroyed(Entity<UndergroundRockComponent> ent, ref DestructionEventArgs args)
    {
        HandleRockRemoved(ent);
    }

    private void OnRockTerminating(Entity<UndergroundRockComponent> ent, ref EntityTerminatingEvent args)
    {
        HandleRockRemoved(ent);
    }

    private void HandleRockRemoved(Entity<UndergroundRockComponent> ent)
    {
        if (_transform.GetGrid(ent.Owner.ToCoordinates()) is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
        {
            return;
        }

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, grid, ent.Owner.ToCoordinates());

        // Spawn dug marker at the mined tile
        SpawnDugMarker(gridUid, grid, tileIndices);

        // Spawn rocks around the newly cleared tile
        SpawnRocksAround(gridUid, grid, tileIndices);
    }

    private void SpawnDugMarker(EntityUid gridUid, MapGridComponent grid, Vector2i tileIndices)
    {
        // Check if a marker already exists
        if (HasDugMarker(gridUid, grid, tileIndices))
            return;

        var coords = _mapSystem.GridTileToLocal(gridUid, grid, tileIndices);
        Spawn("CMUUndergroundDugMarker", coords);
    }

    private bool HasDugMarker(EntityUid gridUid, MapGridComponent grid, Vector2i tileIndices)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (_dugMarkerQuery.HasComp(uid))
                return true;
        }

        return false;
    }

    private bool HasRock(EntityUid gridUid, MapGridComponent grid, Vector2i tileIndices)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (_rockQuery.HasComp(uid))
                return true;
        }

        return false;
    }

    #endregion

    #region Cave-in Support Check

    private bool IsTileSupported(EntityUid gridUid, MapGridComponent grid, Vector2i tileIndices)
    {
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                var check = tileIndices + new Vector2i(dx, dy);
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, check);
                while (anchored.MoveNext(out var uid))
                {
                    if (_beamQuery.HasComp(uid))
                        return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Underground Suffocation

    // 4-directional offsets for BFS (movement is 4-dir)
    private static readonly Vector2i[] CardinalOffsets =
    {
        new(0, 1), new(0, -1), new(1, 0), new(-1, 0),
    };

    private void CheckUndergroundSuffocation()
    {
        // Step 1: Collect entrance tiles per underground grid
        var entrancesByGrid = new Dictionary<EntityUid, List<Vector2i>>();
        var undergroundGrids = new Dictionary<EntityUid, MapGridComponent>();

        var entranceQuery = EntityQueryEnumerator<UndergroundEntranceComponent, TransformComponent>();
        while (entranceQuery.MoveNext(out _, out var entrance, out var xform))
        {
            if (!entrance.IsUnderground)
                continue;

            if (_transform.GetGrid(xform.Coordinates) is not { } gridUid)
                continue;

            if (!_undergroundQuery.HasComp(gridUid))
                continue;

            if (!TryComp(gridUid, out MapGridComponent? grid))
                continue;

            var tile = _mapSystem.CoordinatesToTile(gridUid, grid, xform.Coordinates);

            if (!entrancesByGrid.TryGetValue(gridUid, out var list))
            {
                list = new List<Vector2i>();
                entrancesByGrid[gridUid] = list;
                undergroundGrids[gridUid] = grid;
            }

            list.Add(tile);
        }

        // Step 2: BFS flood-fill from entrance tiles to find all reachable tiles per grid
        var reachableByGrid = new Dictionary<EntityUid, HashSet<Vector2i>>();

        foreach (var (gridUid, entranceTiles) in entrancesByGrid)
        {
            var grid = undergroundGrids[gridUid];
            var reachable = new HashSet<Vector2i>();
            var queue = new Queue<Vector2i>();

            foreach (var tile in entranceTiles)
            {
                if (reachable.Add(tile))
                    queue.Enqueue(tile);
            }

            while (queue.TryDequeue(out var current))
            {
                foreach (var offset in CardinalOffsets)
                {
                    var neighbor = current + offset;

                    if (!reachable.Add(neighbor))
                        continue;

                    // Check tile exists and has no rock
                    var tileRef = _mapSystem.GetTileRef(gridUid, grid, neighbor);
                    if (tileRef.Tile.IsEmpty)
                    {
                        reachable.Remove(neighbor);
                        continue;
                    }

                    if (HasRock(gridUid, grid, neighbor))
                    {
                        reachable.Remove(neighbor);
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            reachableByGrid[gridUid] = reachable;
        }

        // Step 3: Damage alive entities on underground tiles not reachable from any entrance
        var suffocateDamage = new DamageSpecifier();
        suffocateDamage.DamageDict.Add("Asphyxiation", 50);

        var crushDamage = new DamageSpecifier();
        crushDamage.DamageDict.Add("Blunt", 50);

        var mobQuery = EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var mobUid, out var mobState, out _, out var mobXform))
        {
            if (_mobState.IsDead(mobUid, mobState))
                continue;

            if (_transform.GetGrid(mobXform.Coordinates) is not { } mobGridUid)
                continue;

            if (!_undergroundQuery.HasComp(mobGridUid))
                continue;

            if (!TryComp(mobGridUid, out MapGridComponent? mobGrid))
                continue;

            var mobTile = _mapSystem.CoordinatesToTile(mobGridUid, mobGrid, mobXform.Coordinates);

            // If this grid has reachable tiles and the mob is on one, it is safe
            if (reachableByGrid.TryGetValue(mobGridUid, out var reachable) && reachable.Contains(mobTile))
                continue;

            // Sealed cavern: apply damage
            if (_xenoQuery.HasComp(mobUid))
            {
                _damageable.TryChangeDamage(mobUid, crushDamage);
                _popup.PopupEntity(Loc.GetString("cmu-underground-sealed-xeno"), mobUid, mobUid, PopupType.SmallCaution);
            }
            else
            {
                _damageable.TryChangeDamage(mobUid, suffocateDamage);
                _popup.PopupEntity(Loc.GetString("cmu-underground-suffocating"), mobUid, mobUid, PopupType.SmallCaution);
            }
        }
    }

    #endregion

    #region Area Ceiling Level

    /// <summary>
    /// Returns the static ceiling level for the area at the given surface coordinates.
    /// 0 = open sky, 1-2 = roofed, 3-4 = cave/out of bounds.
    /// </summary>
    private int GetAreaCeilingLevel(EntityCoordinates surfaceCoordinates)
    {
        if (!_area.TryGetArea(surfaceCoordinates, out var area, out _))
            return 0;

        if (!area.Value.Comp.OB)
            return 4;
        if (!area.Value.Comp.CAS)
            return 3;
        if (!area.Value.Comp.SupplyDrop || !area.Value.Comp.MortarFire)
            return 2;
        if (!area.Value.Comp.MortarPlacement || !area.Value.Comp.Lasing ||
            !area.Value.Comp.Medevac || !area.Value.Comp.Paradropping)
            return 1;

        return 0;
    }

    #endregion
}
