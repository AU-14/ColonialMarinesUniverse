// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.CameraShake;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 1: the structural support graph.
///
/// A structure carrying <see cref="StructuralSupportComponent"/> is "supported" only if it can trace a
/// cardinal path back to an anchor within the available cantilever budget. Anchors are either explicitly
/// flagged (<see cref="StructuralSupportComponent.IsAnchor"/>) or auto-detected: a support sitting on a
/// solid (non-empty) tile of the lowest z-level (a map with no <see cref="CMUZLevelMapComponent.MapBelow"/>)
/// is rooted in bedrock.
///
/// Phase 1 is intentionally NON-DESTRUCTIVE: it only computes <see cref="StructuralSupportComponent.Supported"/>
/// (visible in ViewVariables), logs transitions, and popups newly-unsupported structures. Collapse
/// scheduling (the 8s warning, bury/shake on lower z, despawn+debris on upper z) lands in a later phase.
/// </summary>
public sealed class ZLevelSupportSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RMCCameraShakeSystem _shake = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    // Per-map cooldown so a cascading floor collapse (many structures at once) logs/alerts admins once, not per
    // tile. Maps the collapsing level -> the time after which the next collapse there alerts again.
    private readonly Dictionary<EntityUid, TimeSpan> _nextCollapseAlert = new();
    private static readonly TimeSpan CollapseAlertCooldown = TimeSpan.FromSeconds(3);

    /// <summary>Crash SFX when an unsupported structure caves in (guarded against a missing path).</summary>
    private static readonly SoundSpecifier CollapseSound = new SoundPathSpecifier("/Audio/Effects/explosion3.ogg");

    /// <summary>Debris that rains onto the level below when a structure caves in.</summary>
    private const string DebrisProto = "AU14RockDebris";

    // Telegraph: while structures count down to collapse, send players on their levels a periodic tremor.
    private TimeSpan _nextTremor;
    private static readonly TimeSpan TremorInterval = TimeSpan.FromSeconds(0.9);
    private readonly HashSet<EntityUid> _tremorMaps = new();

    private static readonly Vector2i[] Cardinals =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;

    // Grids whose support graph needs recomputing next tick (debounce so a multi-entity build only solves once).
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private readonly List<EntityUid> _processing = new();

    // Structural entities that have lost support: maps entity uid -> time at which it will collapse.
    // Cleared if the entity regains support before the deadline (counterplay via building more pillars/anchors).
    private readonly Dictionary<EntityUid, TimeSpan> _pendingUnsupported = new();

    // Support entities whose collapse should be executed this tick (populated transiently during Update).
    private readonly List<EntityUid> _toCollapse = new();

    /// <summary>Seconds a structure remains standing after losing its last support before collapsing.</summary>
    private const float CollapseWarningSeconds = 5f;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();

        SubscribeLocalEvent<StructuralSupportComponent, MapInitEvent>(OnSupportMapInit);
        SubscribeLocalEvent<StructuralSupportComponent, ComponentShutdown>(OnSupportShutdown);
        SubscribeLocalEvent<StructuralSupportComponent, AnchorStateChangedEvent>(OnSupportAnchorChanged);
    }

    private void OnSupportMapInit(Entity<StructuralSupportComponent> ent, ref MapInitEvent args)
        => MarkGridDirty(ent);

    private void OnSupportShutdown(Entity<StructuralSupportComponent> ent, ref ComponentShutdown args)
        => MarkGridDirty(ent);

    private void OnSupportAnchorChanged(Entity<StructuralSupportComponent> ent, ref AnchorStateChangedEvent args)
        => MarkGridDirty(ent);

    /// <summary>Queues the grid the entity currently sits on for a recompute next update.</summary>
    public void MarkGridDirty(EntityUid uid)
    {
        var grid = Transform(uid).GridUid;
        if (grid != null)
            _dirtyGrids.Add(grid.Value);
    }

    public override void Update(float frameTime)
    {
        // Recompute support graphs for grids that changed this tick.
        if (_dirtyGrids.Count > 0)
        {
            _processing.Clear();
            _processing.AddRange(_dirtyGrids);
            _dirtyGrids.Clear();

            foreach (var grid in _processing)
            {
                if (_gridQuery.TryComp(grid, out var gridComp))
                    RecomputeGrid((grid, gridComp));
            }
        }

        // Collapse structures that have been unsupported long enough.
        if (_pendingUnsupported.Count > 0)
        {
            var now = _timing.CurTime;
            _toCollapse.Clear();

            foreach (var (uid, collapseAt) in _pendingUnsupported)
            {
                if (now >= collapseAt)
                    _toCollapse.Add(uid);
            }

            foreach (var uid in _toCollapse)
            {
                _pendingUnsupported.Remove(uid);
                if (Deleted(uid))
                    continue;
                // Skip if it lost its support component (already collapsed as part of another tile's drop), or if
                // it was shored up before the deadline (it would then have been removed from the dict).
                if (!TryComp<StructuralSupportComponent>(uid, out var sup) || sup.Supported)
                    continue;

                CollapseUnsupportedStructure(uid);
            }
        }

        // Telegraph the impending cave-in: a small, repeating tremor for everyone on a level that has a structure
        // counting down, so you feel it coming and have a chance to shore it up before it drops.
        if (_pendingUnsupported.Count > 0 && _timing.CurTime >= _nextTremor)
        {
            _nextTremor = _timing.CurTime + TremorInterval;

            _tremorMaps.Clear();
            foreach (var uid in _pendingUnsupported.Keys)
            {
                if (!Deleted(uid) && Transform(uid).MapUid is { } map)
                    _tremorMaps.Add(map);
            }

            if (_tremorMaps.Count > 0)
            {
                var actors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
                while (actors.MoveNext(out var actor, out var actorXform))
                {
                    if (actorXform.MapUid is { } am && _tremorMaps.Contains(am))
                        _shake.ShakeCamera(actor.Owner, 2, 1);
                }
            }
        }
    }

    /// <summary>
    /// Multi-source cantilever BFS from every anchor on the grid. Each tile carries a remaining "budget";
    /// stepping onto a plain floor costs 1, stepping onto a vertical support / anchor refreshes the budget to
    /// that support's span. Any support entity the flood never reaches is unsupported.
    /// </summary>
    public void RecomputeGrid(Entity<MapGridComponent> grid)
    {
        // Gather all supports on this grid, indexed by tile, and reset their state.
        var byTile = new Dictionary<Vector2i, Entity<StructuralSupportComponent>>();
        var previous = new Dictionary<Vector2i, bool>();

        var query = EntityQueryEnumerator<StructuralSupportComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != grid.Owner)
                continue;

            var tile = _map.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);
            byTile[tile] = (uid, comp);
            previous[tile] = comp.Supported;
            comp.Supported = false;
        }

        if (byTile.Count == 0)
            return;

        // Seed the flood from anchors.
        var queue = new Queue<(Vector2i Tile, int Budget)>();
        var best = new Dictionary<Vector2i, int>();

        foreach (var (tile, ent) in byTile)
        {
            if (!TryGetSeedBudget(ent, grid, tile, out var budget))
                continue;

            if (!best.TryGetValue(tile, out var existing) || existing < budget)
            {
                best[tile] = budget;
                queue.Enqueue((tile, budget));
            }
        }

        // Flood.
        while (queue.TryDequeue(out var node))
        {
            // A stale entry (a better budget was queued later) - skip.
            if (!best.TryGetValue(node.Tile, out var cur) || cur != node.Budget)
                continue;

            if (byTile.TryGetValue(node.Tile, out var here))
                here.Comp.Supported = true;

            foreach (var dir in Cardinals)
            {
                var next = node.Tile + dir;
                if (!byTile.TryGetValue(next, out var nEnt))
                    continue;

                // Vertical supports / anchors relay their full span; floors decrement.
                var nextBudget = nEnt.Comp.IsAnchor || nEnt.Comp.IsVerticalSupport
                    ? nEnt.Comp.CantileverSpan
                    : node.Budget - 1;

                if (nextBudget < 0)
                    continue;

                if (best.TryGetValue(next, out var seen) && seen >= nextBudget)
                    continue;

                best[next] = nextBudget;
                queue.Enqueue((next, nextBudget));
            }
        }

        // Report and schedule/cancel collapses. Only UPPER z-levels (depth > 0) are ever collapsed by the support
        // graph - the ground and everything below it rest on real ground and are handled by cave-ins instead, so
        // a ground/underground structure must never be flung around by this system (that was the "constantly
        // collapsing regardless of support" bug). Scheduling is based on the CURRENT unsupported state, not just
        // the supported->unsupported transition, so a structure that is unsupported from the moment it is built
        // still collapses (that was the "actual lack of support does not collapse" bug).
        var now = _timing.CurTime;
        var isUpperLevel = !IsGroundOrBelow(Transform(grid.Owner).MapUid);

        foreach (var (tile, ent) in byTile)
        {
            if (!isUpperLevel || ent.Comp.Supported)
            {
                // Ground/underground, or genuinely supported: never pending.
                _pendingUnsupported.Remove(ent.Owner);
                continue;
            }

            // Upper-z and currently unsupported: schedule a collapse if not already counting down.
            if (!_pendingUnsupported.ContainsKey(ent.Owner))
            {
                _pendingUnsupported[ent.Owner] = now + TimeSpan.FromSeconds(CollapseWarningSeconds);

                // Popup only on the supported -> unsupported transition, to avoid spamming every recompute.
                if (previous[tile])
                {
                    Log.Info($"[zsupport] {ToPrettyString(ent.Owner)} at {tile} lost structural support.");
                    _popup.PopupCoordinates(
                        Loc.GetString("au-zsupport-unsupported"),
                        Transform(ent.Owner).Coordinates,
                        PopupType.MediumCaution);
                }
            }
        }

        // The level directly ABOVE depends on this one (beams here hold up its floors), so queue its supports for
        // a recompute whenever this grid changes. Propagation is unconditional: a destroyed beam may not flip any
        // Supported state on its own level yet still unroot the floor it was holding up one level higher. This
        // walks one level up per change and stops at the top (or at a level with no supports), so it terminates.
        MarkAboveDirty(grid.Owner);
    }

    /// <summary>
    /// Queues every support on the z-level directly above <paramref name="grid"/> for a recompute, so upper-z
    /// structures re-evaluate when the level below them changes (a destroyed column unroots what it held up).
    /// </summary>
    private void MarkAboveDirty(EntityUid grid)
    {
        var mapUid = Transform(grid).MapUid;
        if (mapUid == null || !_zMapQuery.TryComp(mapUid.Value, out var z) || z.MapAbove is not { } above)
            return;

        var query = EntityQueryEnumerator<StructuralSupportComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == above && xform.GridUid is { } g)
                _dirtyGrids.Add(g);
        }
    }

    /// <summary>
    /// Handles an upper-z structure that remained unsupported past its warning window: the floor gives way. The
    /// tile it sits on is pulled out and EVERYTHING anchored on that tile - the structure itself, the floor's
    /// invisible support marker, and anything else built there - falls to the level directly below (where there
    /// is now no floor under it). Tile-floor markers are deleted (which removes their floor tile); the structure
    /// also gets a small horizontal shove so it tumbles as it drops. This is the "tiles and entities fall when an
    /// upper level collapses" behaviour.
    /// </summary>
    private void CollapseUnsupportedStructure(EntityUid uid)
    {
        Log.Info($"[zsupport] {ToPrettyString(uid)} lost structural support - the floor gives way.");

        var xform = Transform(uid);
        var coords = xform.Coordinates;
        var mapUid = xform.MapUid;

        // Accountability: log the collapse and alert admins, attributing it to the nearest player on the level
        // (most likely the one who knocked out the support below). Throttled per map so a whole floor caving in
        // is one alert, not dozens.
        if (mapUid is { } collapseMap)
        {
            var now = _timing.CurTime;
            if (!_nextCollapseAlert.TryGetValue(collapseMap, out var next) || now >= next)
            {
                _nextCollapseAlert[collapseMap] = now + CollapseAlertCooldown;
                var worldPos = _transform.ToMapCoordinates(coords).Position;
                var culprit = DescribeNearestPlayer(collapseMap, worldPos);
                _adminLog.Add(LogType.Action, LogImpact.High,
                    $"Upper z-level collapse: {ToPrettyString(uid)} lost structural support on {ToPrettyString(collapseMap)}; likely caused by {culprit}.");
                _chat.SendAdminAlert(Loc.GetString("au-zsupport-admin-alert", ("culprit", culprit)));
            }
        }

        PlayCollapseEffects(coords, mapUid);

        var belowMap = mapUid != null && _zMapQuery.TryComp(mapUid.Value, out var zMap) ? zMap.MapBelow : null;

        if (xform.GridUid is { } gridUid && _gridQuery.TryComp(gridUid, out var grid))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, coords);
            DropTileAndContents((gridUid, grid), tile, belowMap);
        }
        else
        {
            // No grid (shouldn't happen for a built structure) - just stop tracking it.
            RemComp<StructuralSupportComponent>(uid);
        }
    }

    /// <summary>
    /// Pulls the floor <paramref name="tile"/> out and drops everything anchored on it to <paramref name="belowMap"/>
    /// at the same world position. Tile-floor markers are deleted (removing their tile); staircases are spared;
    /// other structures are unanchored, un-tracked, moved down, and given a small tumble shove.
    /// </summary>
    private void DropTileAndContents(Entity<MapGridComponent> grid, Vector2i tile, EntityUid? belowMap)
    {
        MapComponent? belowMapComp = null;
        if (belowMap != null)
            TryComp(belowMap.Value, out belowMapComp);

        var worldPos = _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, tile)).Position;

        // Snapshot first: unanchoring/moving mutates the grid's anchored set.
        var anchored = new List<EntityUid>(_map.GetAnchoredEntities(grid.Owner, grid.Comp, tile));
        foreach (var ent in anchored)
        {
            // The floor's invisible support marker: deleting it removes its tile (TileApplierSystem) - it has no
            // body to fall, so don't move it.
            if (HasComp<TileFloorSupportComponent>(ent))
            {
                QueueDel(ent);
                continue;
            }

            // Never drop a staircase.
            if (HasComp<CMUZLevelHighGroundComponent>(ent))
                continue;

            if (TryComp<TransformComponent>(ent, out var exf) && exf.Anchored)
                _transform.Unanchor(ent, exf);

            // No longer a structural participant once it has broken loose.
            RemComp<StructuralSupportComponent>(ent);

            if (belowMapComp != null)
            {
                var offset = _random.NextAngle().ToVec() * _random.NextFloat(0.2f, 0.8f);
                _transform.SetMapCoordinates(ent, new MapCoordinates(worldPos + offset, belowMapComp.MapId));
                _throwing.TryThrow(ent, offset, baseThrowSpeed: 4f);
            }
        }

        // Pull the floor tile out so the floor visibly collapses (does nothing if it was a void tile already).
        _map.SetTile(grid.Owner, grid.Comp, tile, Tile.Empty);
    }

    /// <summary>A crash sound at the collapse spot plus a brief screenshake for players on that level - so a
    /// cave-in reads as a real, physical event rather than a structure silently vanishing.</summary>
    private void PlayCollapseEffects(EntityCoordinates coords, EntityUid? mapUid)
    {
        // A bad audio path must never crash the support tick.
        try
        {
            _audio.PlayPvs(CollapseSound, coords);
        }
        catch (Exception e)
        {
            Log.Warning($"[zsupport] collapse sfx failed: {e.Message}");
        }

        if (mapUid == null)
            return;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var actor, out var actorXform))
        {
            if (actorXform.MapUid == mapUid)
                _shake.ShakeCamera(actor.Owner, 4, 2);
        }

        // Debris rains onto the level directly below, with its own thud + shake, so the cave-in reads on both
        // levels (rubble actually lands where it would fall).
        if (TryComp<CMUZLevelMapComponent>(mapUid.Value, out var zMap) &&
            zMap.MapBelow is { } below &&
            TryComp<MapComponent>(below, out var belowMap))
        {
            var worldPos = _transform.ToMapCoordinates(coords).Position;
            var belowCoords = new MapCoordinates(worldPos, belowMap.MapId);

            var debris = Spawn(DebrisProto, belowCoords);
            try
            {
                _audio.PlayPvs(CollapseSound, debris);
            }
            catch (Exception e)
            {
                Log.Warning($"[zsupport] below-level collapse sfx failed: {e.Message}");
            }

            var belowActors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (belowActors.MoveNext(out var actor, out var actorXform))
            {
                if (actorXform.MapUid == below)
                    _shake.ShakeCamera(actor.Owner, 3, 2);
            }
        }
    }

    /// <summary>
    /// Decides whether a support tile is a ROOT of the graph and, if so, the budget (cantilever reach) to seed
    /// the flood with. Roots are:
    ///  - explicit anchors (seed = own span);
    ///  - a support resting on a solid floor tile on the GROUND level or below (depth &lt;= 0) - the surface and
    ///    everything under it sit on real ground, so they are stable on a solid tile (this also keeps the
    ///    underground itself from collapsing through the support graph; cave-ins are handled separately). Seed =
    ///    own span;
    ///  - the UPPER-Z rule: any support that has a vertical support beam directly beneath it on the level below
    ///    is held up by that beam, and the seed budget is the BEAM's span (its quality). This is the whole
    ///    "build a beam below to hold up the floor above" mechanic - and because a beam on an upper level is
    ///    itself only a root if there is another beam below IT, removing a lower beam unroots everything above
    ///    and the collapse cascades upward (propagated each tick via <see cref="MarkAboveDirty"/>).
    /// Lower/underground levels never collapse from "missing" support because they root on solid ground.
    /// </summary>
    private bool TryGetSeedBudget(Entity<StructuralSupportComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, out int budget)
    {
        budget = 0;
        var mapUid = Transform(grid.Owner).MapUid;

        if (ent.Comp.IsAnchor)
        {
            budget = ent.Comp.CantileverSpan;
            return true;
        }

        var onSolid = _map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
        if (onSolid && IsGroundOrBelow(mapUid))
        {
            budget = ent.Comp.CantileverSpan;
            return true;
        }

        // Upper-z: held up by a beam directly below - reach is the BEAM's span (its quality tier).
        if (mapUid != null &&
            _zMapQuery.TryComp(mapUid.Value, out var zMap) &&
            zMap.MapBelow is { } below &&
            TryGetSupportSpanBelow(below, _transform.GetWorldPosition(ent.Owner), out var belowSpan))
        {
            budget = belowSpan;
            return true;
        }

        return false;
    }

    /// <summary>
    /// True if the level is the ground/surface (depth 0) or underground (depth &lt; 0), i.e. NOT an upper z-level.
    /// A map with no z-level data is a plain single ground map. Upper levels (depth &gt; 0) must be held up by a
    /// beam on the level below instead of by resting on a floor.
    /// </summary>
    private bool IsGroundOrBelow(EntityUid? mapUid)
    {
        if (mapUid == null)
            return true;

        return !_zMapQuery.TryComp(mapUid.Value, out var z) || z.Depth <= 0;
    }

    /// <summary>
    /// Best-effort attribution for a collapse: the nearest player on <paramref name="mapUid"/> to
    /// <paramref name="worldPos"/> (most likely the one who removed the support). Returns a pretty string for the
    /// admin log / alert, or a "no nearby player" note if the level is empty.
    /// </summary>
    private string DescribeNearestPlayer(EntityUid mapUid, Vector2 worldPos)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            var dist = (_transform.GetWorldPosition(uid) - worldPos).LengthSquared();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = uid;
            }
        }

        return best is { } b ? ToPrettyString(b).ToString() : "no nearby player";
    }

    /// <summary>
    /// Returns the largest cantilever span of any vertical support / anchor at the same world position on the
    /// level below, or false if there is none. That span becomes the seed budget for the tile above.
    /// </summary>
    private bool TryGetSupportSpanBelow(EntityUid belowMap, Vector2 worldPos, out int span)
    {
        span = 0;
        if (!TryComp<MapComponent>(belowMap, out var belowMapComp))
            return false;

        var coords = new MapCoordinates(worldPos, belowMapComp.MapId);
        if (!_mapManager.TryFindGridAt(coords, out var belowGridUid, out var belowGrid))
            return false;

        var tile = _map.TileIndicesFor(belowGridUid, belowGrid, coords);
        var best = -1;
        foreach (var anchored in _map.GetAnchoredEntities(belowGridUid, belowGrid, tile))
        {
            if (TryComp<StructuralSupportComponent>(anchored, out var sup) && (sup.IsVerticalSupport || sup.IsAnchor))
                best = Math.Max(best, sup.CantileverSpan);
        }

        if (best < 0)
            return false;

        span = best;
        return true;
    }
}
