using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaTrophySystem _trophies = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private CMUZLevelShootingSystem _zLevelShooting = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, YautjaLeapActionEvent>(OnLeap);
        SubscribeLocalEvent<YautjaComponent, YautjaLeapDoAfterEvent>(OnLeapDoAfter);
        SubscribeLocalEvent<YautjaLeapingComponent, StopThrowEvent>(OnLeapStopped);
        SubscribeLocalEvent<YautjaLeapingComponent, ComponentRemove>(OnLeapComponentRemoved);
        SubscribeLocalEvent<YautjaComponent, YautjaButcherActionEvent>(OnButcher);
        SubscribeLocalEvent<YautjaComponent, YautjaMarkForHuntActionEvent>(OnMarkForHunt);
    }

    public void GrantActions(Entity<YautjaComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.LeapAction, ent.Comp.LeapActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.MarkForHuntAction, ent.Comp.MarkForHuntActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.ButcherAction, ent.Comp.ButcherActionId);
    }

    public void RemoveActions(Entity<YautjaComponent> ent)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.LeapAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.MarkForHuntAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.ButcherAction);
    }

    private void OnButcher(Entity<YautjaComponent> ent, ref YautjaButcherActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || _mob.IsIncapacitated(ent.Owner))
            return;

        args.Handled = _trophies.TryOpenButcherDialog(ent.Owner);
    }

    private void OnMarkForHunt(Entity<YautjaComponent> ent, ref YautjaMarkForHuntActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        if (!_power.TryGetWornBracer(ent.Owner, out var bracer))
            return;

        if (_marks.IsMarkedBy(args.Target, YautjaMarkKind.Prey, ent.Owner))
        {
            args.Handled = _marks.TryClearMark(args.Target, YautjaMarkKind.Prey, ent.Owner, showPreyRemoved: true);
            return;
        }

        args.Handled = _marks.TryMark(bracer, ent.Owner, args.Target, YautjaMarkKind.Prey, null);
    }
    private void OnLeap(Entity<YautjaComponent> ent, ref YautjaLeapActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || _mob.IsIncapacitated(ent.Owner))
            return;

        args.Handled = true;

        var origin = _transform.GetMapCoordinates(ent.Owner);
        var target = _transform.ToMapCoordinates(args.Target);

        // CMU14: CM-SS13 leaps to whichever level the player is looking at (yaut_actions.dm:461). The Z aim system
        // already tracks the level under the cursor, so "look up and click a tile" is the whole interaction - no
        // separate Leap Up/Down action. zOffset is 0 when aiming at the hunter's own level.
        var zOffset = _zLevelShooting.GetShotOffset(ent.Owner);

        MapCoordinates landing;
        var windup = ent.Comp.LeapWindup;
        if (zOffset != 0)
        {
            if (Transform(ent.Owner).MapUid is not { } originMap ||
                !_zLevels.TryMapOffset(originMap, zOffset, out var targetMap) ||
                targetMap is not { } destinationMap)
                return;

            landing = new MapCoordinates(target.Position, _transform.GetMapId(destinationMap.Owner));
            windup = zOffset > 0 ? ent.Comp.LeapUpWindup : ent.Comp.LeapDownWindup;
        }
        else
        {
            if (origin.MapId != target.MapId)
                return;

            var direction = ClampLeapDirection(ent, target.Position - origin.Position);
            if (direction == Vector2.Zero)
                return;

            landing = new MapCoordinates(origin.Position + direction, origin.MapId);
        }

        // CMU14: a click on a deck is an EntityCoordinates on that grid, so keep the grid identity and the tile.
        // The world position is only a fallback; landing re-resolves against the grid's transform at that moment.
        NetEntity? destinationGrid = null;
        Vector2? gridLocalPosition = null;
        if (HasComp<MapGridComponent>(args.Target.EntityId))
        {
            destinationGrid = GetNetEntity(args.Target.EntityId);
            gridLocalPosition = args.Target.Position;
        }

        var landingCoords = _transform.ToCoordinates(landing);
        var warning = SpawnAtPosition(ent.Comp.LeapWarningPrototype, landingCoords);

        var doAfter = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            windup,
            new YautjaLeapDoAfterEvent(
                GetNetCoordinates(landingCoords),
                GetNetEntity(warning),
                zOffset,
                destinationGrid,
                gridLocalPosition),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnDamage = true,
            DamageThreshold = FixedPoint2.New(10),
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        // CMU14: a rejected start (another leap is already winding up, so the duplicate is blocked) means no
        // do-after will ever run, and therefore nothing would delete the warning we just spawned. Clear it and
        // leave the action consumed, which is the existing duplicate policy rather than a new cooldown.
        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            if (!Deleted(warning))
                Del(warning);
        }
    }

    private void OnLeapDoAfter(Entity<YautjaComponent> ent, ref YautjaLeapDoAfterEvent args)
    {
        if (args.Warning is { } warning)
            Del(GetEntity(warning));

        if (args.Handled || args.Cancelled || _mob.IsIncapacitated(ent.Owner))
            return;

        var origin = _transform.GetMapCoordinates(ent.Owner);
        var target = _transform.ToMapCoordinates(args.Coordinates);
        if (origin.MapId != target.MapId)
            return;

        var direction = ClampLeapDirection(ent, target.Position - origin.Position);
        if (direction == Vector2.Zero)
            return;

        // CMU14: CM-SS13 plays pred_leap1/pred_leap2 on launch, after the wind-up and before the throw
        // (yaut_actions.dm:468-470). Not on landing, and not when the wind-up is cancelled.
        _audio.PlayPvs(ent.Comp.LeapSound, ent.Owner);

        PrepareLeapCollision((ent.Owner, EnsureComp<YautjaLeapingComponent>(ent.Owner)));
        // The throw movement collides with walls; obstacle slamming must not turn that intentional leap into
        // self-inflicted blunt damage.
        _obstacleSlamming.MakeImmune(ent.Owner, 0.5f);

        // CMU14: a cross-level leap cannot be an animated single-map throw, so it uses the same Z transition the
        // ladder climbs with (CMUZLevelsSystem.TryMove), landing on the destination's world position.
        if (args.ZOffset != 0)
        {
            var landingPosition = target.Position;
            EntityUid? destinationGrid = null;
            if (args.DestinationGrid is { } netGrid &&
                TryGetEntity(netGrid, out var gridUid) &&
                !Deleted(gridUid.Value) &&
                TryComp<MapGridComponent>(gridUid.Value, out var grid) &&
                args.GridLocalPosition is { } localPosition &&
                grid.LocalAABB.Contains(localPosition))
            {
                // resolve now, not at targeting time: the deck may have moved during the wind-up
                landingPosition = _transform.ToMapCoordinates(new EntityCoordinates(gridUid.Value, localPosition)).Position;
                destinationGrid = gridUid.Value;
            }

            // CMU14: this branch does not go through ThrowingSystem, so StopThrowEvent never fires and the
            // collision masks prepared above would stay leap-permissive - letting the hunter walk through
            // impassable terrain and mobs. End the leap on both the success and failure paths.
            if (!_zLevels.TryMove(ent.Owner, args.ZOffset, worldPosition: landingPosition))
            {
                FinishLeapWithoutThrow(ent.Owner);
                return;
            }

            if (destinationGrid is { } deck && args.GridLocalPosition is { } tile)
                _transform.SetCoordinates(ent.Owner, new EntityCoordinates(deck, tile));

            // CMU14: a leap is a controlled arrival, not a fall. Without settling z-physics here the hunter keeps the
            // transition's z state, WakeZPhysics finds no support, and the resulting touchdown raises
            // CMUZLevelHitEvent - whose handler both knocks the victim down and applies Blunt fall damage
            // (OnFallDamage, CMUSharedZLevelsSystem.Movement.cs:191). The ladder settles identically after climbing.
            if (TryComp<CMUZPhysicsComponent>(ent.Owner, out var zPhysics))
            {
                _zLevels.SetZVelocity((ent.Owner, zPhysics), 0f);
                _zLevels.SetZLocalPosition((ent.Owner, zPhysics), 0f);
            }

            RemComp<CMUZFallingComponent>(ent.Owner);

            FinishLeapWithoutThrow(ent.Owner);

            args.Handled = true;
            return;
        }

        _throwing.TryThrow(
            ent.Owner,
            direction,
            ent.Comp.LeapThrowSpeed,
            ent.Owner,
            animated: true,
            compensateFriction: true);

        args.Handled = true;
    }

    /// <summary>
    /// CMU14: ends a leap that never reaches ThrowingSystem (the cross-level path), restoring the collision masks
    /// itself. RestoreLeapCollision clears its saved masks, and OnLeapComponentRemoved calls it again for the deferred
    /// removal below, so a second pass is a no-op rather than a double restore.
    /// </summary>
    private void FinishLeapWithoutThrow(EntityUid uid)
    {
        if (!TryComp<YautjaLeapingComponent>(uid, out var leap))
            return;

        RestoreLeapCollision((uid, leap));
        RemCompDeferred<YautjaLeapingComponent>(uid);
    }

    private void OnLeapStopped(Entity<YautjaLeapingComponent> ent, ref StopThrowEvent args)
    {
        RestoreLeapCollision(ent);
        RemCompDeferred<YautjaLeapingComponent>(ent.Owner);
    }

    private void OnLeapComponentRemoved(Entity<YautjaLeapingComponent> ent, ref ComponentRemove args)
    {
        RestoreLeapCollision(ent);
    }

    private void PrepareLeapCollision(Entity<YautjaLeapingComponent> ent)
    {
        if (!TryComp(ent.Owner, out FixturesComponent? fixtures))
            return;

        ent.Comp.OriginalCollisionMasks.Clear();
        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            ent.Comp.OriginalCollisionMasks[fixtureId] = fixture.CollisionMask;
            _physics.SetCollisionMask(
                ent.Owner,
                fixtureId,
                fixture,
                GetLeapCollisionMask(fixture.CollisionMask),
                fixtures);
        }
    }

    private void RestoreLeapCollision(Entity<YautjaLeapingComponent> ent)
    {
        if (!TryComp(ent.Owner, out FixturesComponent? fixtures))
            return;

        foreach (var (fixtureId, originalMask) in ent.Comp.OriginalCollisionMasks)
        {
            if (!fixtures.Fixtures.TryGetValue(fixtureId, out var fixture))
                continue;

            _physics.SetCollisionMask(ent.Owner, fixtureId, fixture, originalMask, fixtures);
        }

        ent.Comp.OriginalCollisionMasks.Clear();
    }

    public static int GetLeapCollisionMask(int originalMask)
    {
        const int passableDuringLeap = (int) (CollisionGroup.MidImpassable |
                                               CollisionGroup.HighImpassable |
                                               CollisionGroup.LowImpassable |
                                               CollisionGroup.MobCollision);
        return originalMask & ~passableDuringLeap;
    }

    private static Vector2 ClampLeapDirection(Entity<YautjaComponent> ent, Vector2 direction)
    {
        if (direction == Vector2.Zero)
            return direction;

        var distance = direction.Length();
        return distance > ent.Comp.LeapMaxRange
            ? Vector2.Normalize(direction) * ent.Comp.LeapMaxRange
            : direction;
    }
}
