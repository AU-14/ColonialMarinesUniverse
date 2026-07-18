// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
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
/// scheduling (the 8s warning, lower-z effects, despawn+debris on upper z) lands in a later phase.
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
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    // Per-map cooldown so a cascading floor collapse (many structures at once) logs/alerts admins once, not per
    // tile. Maps the collapsing level -> the time after which the next collapse there alerts again.
    private readonly Dictionary<EntityUid, TimeSpan> _nextCollapseAlert = new();
    private static readonly TimeSpan CollapseAlertCooldown = TimeSpan.FromSeconds(3);

    // Exact attribution: the last PLAYER to damage a support on each map, with the time. Used to name the real
    // culprit in the collapse log/alert (e.g. whoever shot the beam out) instead of just "nearest player".
    private readonly Dictionary<EntityUid, (EntityUid Culprit, TimeSpan Time)> _lastSupportDamager = new();
    private static readonly TimeSpan AttributionWindow = TimeSpan.FromSeconds(15);

    /// <summary>Crash SFX when an unsupported structure caves in (guarded against a missing path).</summary>
    private static readonly SoundSpecifier CollapseSound = new SoundPathSpecifier("/Audio/Effects/explosion3.ogg");

    /// <summary>Debris that rains onto the level below when a structure caves in.</summary>
    private const string DebrisProto = "AU14RockDebris";

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
        SubscribeLocalEvent<StructuralSupportComponent, DamageChangedEvent>(OnSupportDamaged);

        // All of these are keyed by round-scoped uids; drop them with the round so stale entries never accumulate.
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _lastSupportDamager.Clear();
            _nextCollapseAlert.Clear();
            _pendingUnsupported.Clear();
            _dirtyGrids.Clear();
        });
    }

    /// <summary>Records the last player to damage a support on a level, so a resulting collapse names the real culprit.</summary>
    private void OnSupportDamaged(Entity<StructuralSupportComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin || !HasComp<ActorComponent>(origin))
            return;

        if (Transform(ent).MapUid is { } mapUid)
            _lastSupportDamager[mapUid] = (origin, _timing.CurTime);
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
    }

    /// <summary>
    /// Multi-source cantilever BFS from every anchor on the grid. Each tile carries a remaining "budget";
    /// stepping onto a plain floor costs 1, stepping onto a vertical support / anchor refreshes the budget to
    /// that support's span. Any support entity the flood never reaches is unsupported.
    /// </summary>
    public void RecomputeGrid(Entity<MapGridComponent> grid)
    {
        // Gather all supports on this grid, indexed by tile, and reset their state.
        var byTile = new Dictionary<Vector2i, List<Entity<StructuralSupportComponent>>>();
        var previous = new Dictionary<EntityUid, bool>();

        var query = EntityQueryEnumerator<StructuralSupportComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != grid.Owner)
                continue;

            var tile = _map.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);
            if (!byTile.TryGetValue(tile, out var supports))
            {
                supports = new List<Entity<StructuralSupportComponent>>();
                byTile.Add(tile, supports);
            }

            supports.Add((uid, comp));
            previous[uid] = comp.Supported;
            comp.Supported = false;
        }

        if (byTile.Count == 0)
        {
            // Removing the last beam is the most important change to propagate. Returning here without dirtying
            // the level above left its previously-supported floor markers alive forever.
            MarkAboveDirty(grid.Owner);
            return;
        }

        // Seed the flood from anchors.
        var queue = new Queue<(Vector2i Tile, int Budget)>();
        var best = new Dictionary<Vector2i, int>();

        foreach (var (tile, supports) in byTile)
        {
            foreach (var ent in supports)
            {
                if (!TryGetSeedBudget(ent, grid, tile, out var budget))
                    continue;

                if (!best.TryGetValue(tile, out var existing) || existing < budget)
                {
                    best[tile] = budget;
                    queue.Enqueue((tile, budget));
                }
            }
        }

        // Flood.
        while (queue.TryDequeue(out var node))
        {
            // A stale entry (a better budget was queued later) - skip.
            if (!best.TryGetValue(node.Tile, out var cur) || cur != node.Budget)
                continue;

            if (byTile.TryGetValue(node.Tile, out var here))
            {
                foreach (var ent in here)
                    ent.Comp.Supported = true;
            }

            foreach (var dir in Cardinals)
            {
                var next = node.Tile + dir;
                if (!byTile.TryGetValue(next, out var nextSupports))
                    continue;

                // Every colocated support participates. The strongest vertical support refreshes the relay;
                // otherwise crossing the tile consumes one unit of cantilever budget.
                var nextBudget = node.Budget - 1;
                foreach (var nextSupport in nextSupports)
                {
                    if (nextSupport.Comp.IsAnchor || nextSupport.Comp.IsVerticalSupport)
                        nextBudget = Math.Max(nextBudget, nextSupport.Comp.CantileverSpan);
                }

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

        foreach (var (tile, supports) in byTile)
        {
            foreach (var ent in supports)
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
                    if (previous[ent.Owner])
                    {
                        Log.Info($"[zsupport] {ToPrettyString(ent.Owner)} at {tile} lost structural support.");
                        _popup.PopupCoordinates(
                            Loc.GetString("au-zsupport-unsupported"),
                            Transform(ent.Owner).Coordinates,
                            PopupType.MediumCaution);
                    }
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
                var culprit = DescribeCulprit(collapseMap, worldPos, now);
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
                if (TryComp<TransformComponent>(ent, out var supportXform) && supportXform.Anchored)
                    _transform.Unanchor(ent, supportXform);

                QueueDel(ent);
                continue;
            }

            // Never drop a staircase, and never drop an indestructible map-border wall.
            if (HasComp<CMUZLevelHighGroundComponent>(ent) || IsIndestructibleWall(ent))
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

                // Fallen structures are rubble: strip fixture hardness so they can never grind against rocks
                // the cave later generates (or gets buried under) at the same spot. That constant jitter of a
                // solid wall stuck inside a solid rock was a physics-contact drain that degraded server TPS
                // (felt as ever-worsening UI lag) the longer a round ran.
                MakeFallenDebrisNonHard(ent);
            }
        }

        // Pull the floor tile out so the floor visibly collapses (does nothing if it was a void tile already).
        _map.SetTile(grid.Owner, grid.Comp, tile, Tile.Empty);
    }

    /// <summary>True if a player-built floor marker (TileFloorSupport) is anchored on this tile - i.e. the floor
    /// tile was laid by a player, not authored by a mapper.</summary>
    private bool HasPlayerFloorMarker(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var anchored in _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
        {
            if (HasComp<TileFloorSupportComponent>(anchored))
                return true;
        }

        return false;
    }

    /// <summary>An indestructible map-border wall: tagged as a wall but with no Damageable at all (the
    /// CMBaseWallInvincible family). These are map boundaries and must never fall or be moved.</summary>
    private bool IsIndestructibleWall(EntityUid uid)
    {
        return _tag.HasTag(uid, "Wall") && !HasComp<DamageableComponent>(uid);
    }

    /// <summary>Turns a structure that has fallen through a collapsed floor into inert rubble: every fixture's
    /// collision layer/mask is zeroed (non-hard alone still raises contact events, so bullets would still "hit"
    /// the fallen wall - with no collision bits projectiles pass straight through and cave rocks never grind
    /// contacts against it). The networked ZFallenDebris marker makes the client draw it battered.</summary>
    private void MakeFallenDebrisNonHard(EntityUid uid)
    {
        EnsureComp<ZFallenDebrisComponent>(uid);

        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            _physics.SetHard(uid, fixture, false, manager: fixtures);
            _physics.SetCollisionLayer(uid, id, fixture, 0, manager: fixtures);
            _physics.SetCollisionMask(uid, id, fixture, 0, manager: fixtures);
        }
    }

    /// <summary>A crash sound at the collapse spot plus a brief vignette for nearby players.</summary>
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

        // Local effect only: one grey vignette blink for players near the collapse, not map-wide.
        // 🔧 TUNABLE: effect radius in tiles.
        const float effectRange = 33f;
        var collapsePos = _transform.ToMapCoordinates(coords).Position;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out var actorXform))
        {
            if (actorXform.MapUid != mapUid)
                continue;

            if ((_transform.GetWorldPosition(uid) - collapsePos).Length() > effectRange)
                continue;

            RaiseNetworkEvent(new ZCollapseVignetteEvent(), actor.PlayerSession);
        }

        // Debris rains onto the level directly below, with its own thud, so the cave-in reads on both
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

            // Same local radius as the level above: only players near where the debris lands feel the thud.
            var belowActors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (belowActors.MoveNext(out var uid, out var actor, out var actorXform))
            {
                if (actorXform.MapUid != below)
                    continue;

                if ((_transform.GetWorldPosition(uid) - worldPos).Length() > 33f)
                    continue;

                RaiseNetworkEvent(new ZCollapseVignetteEvent(), actor.PlayerSession);
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

        // A solid floor tile on an UPPER level that carries no player-built floor marker is mapper-authored map
        // content (e.g. the USS Bush's upper decks). Authored floors are self-supporting: anything crafted on
        // them roots there and must never cave in. Only PLAYER-built upper floors (which always carry a
        // TileFloorSupport marker) need a beam below.
        if (onSolid && !IsGroundOrBelow(mapUid) && !HasPlayerFloorMarker(grid, tile))
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
    /// Attributes a collapse: prefers the last player who damaged a support on this level within the attribution
    /// window (the exact culprit, e.g. whoever shot the beam out); otherwise falls back to the nearest player.
    /// </summary>
    private string DescribeCulprit(EntityUid mapUid, Vector2 worldPos, TimeSpan now)
    {
        if (_lastSupportDamager.TryGetValue(mapUid, out var last) &&
            now - last.Time <= AttributionWindow &&
            !Deleted(last.Culprit))
        {
            return ToPrettyString(last.Culprit).ToString();
        }

        return DescribeNearestPlayer(mapUid, worldPos);
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
