using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Emp;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Playtest 4: the river's copy. An assimilation that finds no synthetic to meld gives the Early Warning
/// Joe one respawn (never more than one held). The next transition to Dead is turned back: the Joe stays
/// up, and on the next tick it is mended, a large EMP goes off where it fell (its own gear spared), it
/// drops two levels and it is put down on a free tile of its map away from every living player.
/// Deaths that set Dead directly stand: chasms and gibbing never reach the threshold check, and an
/// execution or a suicide lands a lethal blow first, which spends the copy, then sets Dead past it.
/// </summary>
public sealed partial class EWJoeRespawnSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EWJoeAbilitySystem _abilities = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private EWJoeRuleSystem _rule = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, UpdateMobStateEvent>(OnUpdateMobState, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<EWJoeRemovedEvent>(OnJoeRemoved);
    }

    /// <summary>Gives the Joe its one respawn. False when it already holds one.</summary>
    public bool Grant(Entity<EWJoeComponent> joe)
    {
        if (joe.Comp.RespawnReady)
            return false;

        joe.Comp.RespawnReady = true;
        Dirty(joe);
        _alerts.ShowAlert(joe.Owner, joe.Comp.RespawnAlert);
        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-respawn-granted"));
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} was given a respawn (no synthetic to meld)");
        return true;
    }

    /// <summary>
    /// Runs after the threshold system has chosen the state. A lethal transition with the respawn held
    /// (or already under way) is turned back to Alive; the rest happens next tick, outside the damage call.
    /// </summary>
    private void OnUpdateMobState(Entity<EWJoeComponent> ent, ref UpdateMobStateEvent args)
    {
        if (args.State != MobState.Dead || args.Component.CurrentState == MobState.Dead)
            return;

        if (HasComp<EWJoeRespawningComponent>(ent))
        {
            args.State = MobState.Alive;
            return;
        }

        if (!ent.Comp.RespawnReady)
            return;

        args.State = MobState.Alive;
        ent.Comp.RespawnReady = false;
        Dirty(ent);
        _alerts.ClearAlert(ent.Owner, ent.Comp.RespawnAlert);

        var respawning = EnsureComp<EWJoeRespawningComponent>(ent);
        respawning.FellAt = _transform.GetMapCoordinates(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var pending = new List<(EntityUid Uid, EWJoeRespawningComponent Respawning)>();
        var query = EntityQueryEnumerator<EWJoeRespawningComponent, EWJoeComponent>();
        while (query.MoveNext(out var uid, out var respawning, out _))
        {
            pending.Add((uid, respawning));
        }

        foreach (var (uid, respawning) in pending)
        {
            RemComp<EWJoeRespawningComponent>(uid);

            // execution and suicide land a lethal blow, then set Dead directly, past the threshold event:
            // that death stands, and the copy it used is gone with it
            if (_mobState.IsDead(uid))
            {
                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"Early Warning Joe {ToPrettyString(uid):joe} spent its respawn on a death that stood (set Dead directly)");
                continue;
            }

            if (TryComp(uid, out EWJoeComponent? joe))
                Respawn((uid, joe), respawning.FellAt);
        }
    }

    /// <summary>
    /// Mends, pulses, demotes and moves a living Joe. Never revives one that is already dead. Public so
    /// tests can drive it directly.
    /// </summary>
    public void Respawn(Entity<EWJoeComponent> joe, MapCoordinates fellAt)
    {
        if (_mobState.IsDead(joe))
            return;

        _damageable.SetAllDamage(joe.Owner, 0);

        var coordinates = Transform(joe).Coordinates;
        _abilities.EmpAround(joe, coordinates, joe.Comp.RespawnEmpRange, joe.Comp.RespawnEmpEnergy, joe.Comp.RespawnEmpDuration);
        SpawnAtPosition(SharedEmpSystem.EmpPulseEffectPrototype, coordinates);
        _audio.PlayPvs(SharedEmpSystem.EmpSound, coordinates);

        var oldLevel = joe.Comp.Level;
        _rule.LowerLevel((joe, joe.Comp), oldLevel - joe.Comp.RespawnLevelLoss);

        ReleaseHolds(joe);
        var moved = TryFindRefuge(joe, fellAt, out var refuge);
        if (moved)
        {
            _transform.SetMapCoordinates(joe, refuge);
            _transform.AttachToGridOrMap(joe);
        }

        joe.Comp.Respawns++;
        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-respawn-used"));
        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"Early Warning Joe {ToPrettyString(joe):joe} respawned: level {oldLevel} -> {joe.Comp.Level}, moved: {moved} ({refuge})");
    }

    /// <summary>
    /// Nobody comes along: pulls in either direction end, a buckle is undone, and a Joe that fell inside a
    /// locker or vehicle is taken out of it first (CORE-02), so the move does not leave the container
    /// believing it still holds the Joe.
    /// </summary>
    private void ReleaseHolds(EntityUid joe)
    {
        _container.TryRemoveFromContainer(joe, force: true);

        if (TryComp(joe, out PullableComponent? pullable) && pullable.Puller != null)
            _pulling.TryStopPull(joe, pullable);

        if (TryComp(joe, out PullerComponent? puller) && puller.Pulling is { } pulled
            && TryComp(pulled, out PullableComponent? pulledComp))
        {
            _pulling.TryStopPull(pulled, pulledComp);
        }

        if (TryComp(joe, out BuckleComponent? buckle) && buckle.Buckled)
            _buckle.Unbuckle((joe, buckle), null);
    }

    /// <summary>
    /// A free floor tile on the Joe's map, at least <see cref="EWJoeComponent.RespawnClearance"/> from
    /// every living player and from where it fell. When nothing is that clear, the tile that is furthest
    /// from the nearest of them. False when the map has no free tile at all (the Joe stays put).
    /// </summary>
    public bool TryFindRefuge(Entity<EWJoeComponent> joe, MapCoordinates fellAt, out MapCoordinates refuge)
    {
        refuge = fellAt;
        var mapId = fellAt.MapId;
        if (mapId == MapId.Nullspace)
            return false;

        var watchers = new List<Vector2>();
        var actors = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var mob, out var xform))
        {
            if (uid == joe.Owner || xform.MapID != mapId || _mobState.IsDead(uid, mob))
                continue;

            watchers.Add(_transform.GetWorldPosition(xform));
        }

        var grids = new List<(Entity<MapGridComponent> Grid, Vector2i Min, Vector2i Max)>();
        var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var grid, out var gridXform))
        {
            if (gridXform.MapID == mapId && TryGetTileBounds((gridUid, grid), out var min, out var max))
                grids.Add(((gridUid, grid), min, max));
        }

        if (grids.Count == 0)
            return false;

        var clearance = joe.Comp.RespawnClearance;
        var best = float.MinValue;
        var found = false;
        for (var i = 0; i < joe.Comp.RespawnAttempts; i++)
        {
            var (grid, min, max) = PickByArea(grids);
            if (!TryRandomFloor(grid, min, max, out var tile))
                continue;

            var candidate = _transform.ToMapCoordinates(_map.GridTileToLocal(grid, grid, tile.GridIndices));
            var nearest = Vector2.Distance(candidate.Position, fellAt.Position);
            foreach (var watcher in watchers)
            {
                nearest = MathF.Min(nearest, Vector2.Distance(candidate.Position, watcher));
            }

            if (nearest <= best)
                continue;

            best = nearest;
            refuge = candidate;
            found = true;
            if (nearest >= clearance)
                break;
        }

        return found;
    }

    /// <summary>A grid chosen with odds by its tile area: a landed shuttle is not as likely as the planet.</summary>
    private (Entity<MapGridComponent> Grid, Vector2i Min, Vector2i Max) PickByArea(
        List<(Entity<MapGridComponent> Grid, Vector2i Min, Vector2i Max)> grids)
    {
        if (grids.Count == 1)
            return grids[0];

        var total = 0L;
        foreach (var (_, min, max) in grids)
        {
            total += (long) (max.X - min.X + 1) * (max.Y - min.Y + 1);
        }

        var roll = (long) (_random.NextDouble() * total);
        foreach (var entry in grids)
        {
            roll -= (long) (entry.Max.X - entry.Min.X + 1) * (entry.Max.Y - entry.Min.Y + 1);
            if (roll < 0)
                return entry;
        }

        return grids[^1];
    }

    /// <summary>A random tile of <paramref name="grid"/> that is floor (not space) and not blocked for a mob.</summary>
    private bool TryRandomFloor(Entity<MapGridComponent> grid, Vector2i min, Vector2i max, out TileRef tile)
    {
        var index = new Vector2i(_random.Next(min.X, max.X + 1), _random.Next(min.Y, max.Y + 1));
        if (!_map.TryGetTileRef(grid, grid, index, out tile) || _turf.IsSpace(tile))
            return false;

        return !_turf.IsTileBlocked(tile, CollisionGroup.MobMask);
    }

    /// <summary>
    /// Inclusive tile bounds. A planet grid has no LocalAABB (no chunk fixtures), so its tiles are walked
    /// instead, once per respawn, the way the CMU crash-land footprint does it.
    /// </summary>
    private bool TryGetTileBounds(Entity<MapGridComponent> grid, out Vector2i min, out Vector2i max)
    {
        var bounds = grid.Comp.LocalAABB;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            min = new Vector2i((int) MathF.Floor(bounds.Left), (int) MathF.Floor(bounds.Bottom));
            max = new Vector2i((int) MathF.Ceiling(bounds.Right) - 1, (int) MathF.Ceiling(bounds.Top) - 1);
            return true;
        }

        min = new Vector2i(int.MaxValue, int.MaxValue);
        max = new Vector2i(int.MinValue, int.MinValue);
        var tiles = _map.GetAllTilesEnumerator(grid, grid);
        while (tiles.MoveNext(out var tile))
        {
            min = Vector2i.ComponentMin(min, tile.Value.GridIndices);
            max = Vector2i.ComponentMax(max, tile.Value.GridIndices);
        }

        return min.X <= max.X && min.Y <= max.Y;
    }

    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        RemComp<EWJoeRespawningComponent>(args.Joe);
        if (!TryComp(args.Joe, out EWJoeComponent? joe) || !joe.RespawnReady)
            return;

        joe.RespawnReady = false;
        Dirty(args.Joe, joe);
        _alerts.ClearAlert(args.Joe, joe.RespawnAlert);
    }
}

/// <summary>A death was turned back this tick; the respawn finishes on the next update. Server only.</summary>
[RegisterComponent]
public sealed partial class EWJoeRespawningComponent : Component
{
    [DataField]
    public MapCoordinates FellAt;
}
