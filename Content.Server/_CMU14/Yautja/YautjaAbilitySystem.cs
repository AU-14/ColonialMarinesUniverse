using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaTrophySystem _trophies = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, YautjaLeapActionEvent>(OnLeap);
        SubscribeLocalEvent<YautjaComponent, YautjaLeapDoAfterEvent>(OnLeapDoAfter);
        SubscribeLocalEvent<YautjaComponent, YautjaButcherActionEvent>(OnButcher);
        SubscribeLocalEvent<YautjaComponent, YautjaMarkForHuntActionEvent>(OnMarkForHunt);
        SubscribeLocalEvent<YautjaLeapingComponent, StopThrowEvent>(OnLeapStopped);
        SubscribeLocalEvent<YautjaLeapingComponent, ComponentRemove>(OnLeapComponentRemoved);
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

    private void OnLeap(Entity<YautjaComponent> ent, ref YautjaLeapActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || _mob.IsIncapacitated(ent.Owner))
            return;

        args.Handled = true;

        var origin = _transform.GetMapCoordinates(ent.Owner);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        var direction = ClampLeapDirection(ent, target.Position - origin.Position);
        if (direction == Vector2.Zero)
            return;

        var landing = new MapCoordinates(origin.Position + direction, origin.MapId);
        var landingCoords = _transform.ToCoordinates(landing);
        var warning = SpawnAtPosition(ent.Comp.LeapWarningPrototype, landingCoords);

        var doAfter = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            ent.Comp.LeapWindup,
            new YautjaLeapDoAfterEvent(GetNetCoordinates(landingCoords), GetNetEntity(warning)),
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

        _doAfter.TryStartDoAfter(doAfter);
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

        PrepareLeapCollision((ent.Owner, EnsureComp<YautjaLeapingComponent>(ent.Owner)));
        // The throw movement collides with walls; obstacle slamming must not
        // turn that intentional leap into self-inflicted blunt damage.
        _obstacleSlamming.MakeImmune(ent.Owner, 0.5f);

        _throwing.TryThrow(
            ent.Owner,
            direction,
            ent.Comp.LeapThrowSpeed,
            ent.Owner,
            animated: true,
            compensateFriction: true);
        args.Handled = true;
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
}
