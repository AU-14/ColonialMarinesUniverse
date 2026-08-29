using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Physics.Controllers;
using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Content.Shared.Cloning;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.CMU14.TileMovement;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Water;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.Movement;

[TestFixture]
[TestOf(typeof(MoverController))]
public sealed class MovementMergeRegressionTest : MovementTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MovementMergeFrictionDefault
  parent: CMMobHuman
  components:
  - type: MovementSpeedModifier
    baseFriction: 1

- type: entity
  id: MovementMergeFrictionDouble
  parent: CMMobHuman
  components:
  - type: MovementSpeedModifier
    baseFriction: 2

- type: entity
  id: MovementMergeFrictionZero
  parent: CMMobHuman
  components:
  - type: MovementSpeedModifier
    baseFriction: 0

- type: entity
  id: MovementMergePullerSource
  components:
  - type: Puller
    needsHands: false
    throwCooldown: 3
    pullingAlert: Walking

- type: entity
  id: MovementMergePullerTarget

- type: entity
  id: MovementMergeRelayTarget
  parent: BaseMob
";

    [Test]
    public async Task CloneSettingsPreserveForkTileMovementThroughUpstreamSplit()
    {
        await Server.WaitAssertion(() =>
        {
            var mental = SProtoMan.Index<CloningSettingsPrototype>("TraitsMental");
            var physical = SProtoMan.Index<CloningSettingsPrototype>("TraitsPhysical");
            var body = SProtoMan.Index<CloningSettingsPrototype>("Body");
            var clone = SProtoMan.Index<CloningSettingsPrototype>("BaseClone");

            Assert.Multiple(() =>
            {
                Assert.That(mental.Components,
                    Is.SupersetOf(new[] { "CMUTileMovement", "Panic", "NicotineAddiction" }));
                Assert.That(physical.Components,
                    Is.SupersetOf(new[]
                    {
                        "SlowRunner",
                        "RespiratoryStrain",
                        "Anemic",
                        "DrugAllergy",
                        "Epilepsy",
                        "CMUProstheticLeftArm",
                        "CMUProstheticRightArm",
                        "CMUProstheticLeftLeg",
                        "CMUProstheticRightLeg",
                    }));
                Assert.That(body.Components,
                    Is.SupersetOf(new[] { "RandomPrice", "TemperatureDamage", "TemperatureSpeed" }));
                Assert.That(clone.Components,
                    Is.SupersetOf(mental.Components.Concat(physical.Components).Concat(body.Components)));
                Assert.That(clone.Components, Does.Not.Contain("Muted"));
                Assert.That(clone.Components, Does.Not.Contain("PainNumbness"));
                Assert.That(clone.Components, Does.Not.Contain("Clumsy"));
                Assert.That(clone.CopyStatusEffects, Is.True,
                    "status-effect traits use the upstream successor instead of duplicate component copying");
            });
        });
    }

    [Test]
    public async Task RelayForwardsOnceAndCleansBlockedMissingAndStaleMovers()
    {
        var source = SPlayer;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<MovementMergeProbeSystem>();
            var moverController = Server.System<MoverController>();
            target = SEntMan.SpawnEntity(
                "MovementMergeRelayTarget",
                SEntMan.GetComponent<TransformComponent>(source).Coordinates);
            var sourceProbe = SEntMan.EnsureComponent<MovementMergeProbeComponent>(source);
            var targetProbe = SEntMan.EnsureComponent<MovementMergeProbeComponent>(target);

            moverController.SetRelay(source, target);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(source).RelayEntity, Is.EqualTo(target));
                Assert.That(SEntMan.GetComponent<MovementRelayTargetComponent>(target).Source, Is.EqualTo(source));
                Assert.That(moverController.GetEffectiveMover(source), Is.EqualTo(target));
                Assert.That(sourceProbe.EffectiveMoverChanges, Is.EqualTo(1));
                Assert.That(targetProbe.MoveInputEvents, Is.Zero);
                Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(source), Is.True);
                Assert.That(SEntMan.GetComponent<ActiveInputMoverComponent>(target).RelayedFrom, Is.EqualTo(source));
            });
        });

        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Down);
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(source).HeldMoveButtons, Is.EqualTo(MoveButtons.None));
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(target).HeldMoveButtons, Is.EqualTo(MoveButtons.Right));
                Assert.That(SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents, Is.EqualTo(1),
                    "one input transition must be relayed exactly once");
            });
        });

        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Up);
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<InputMoverComponent>(target).HeldMoveButtons, Is.EqualTo(MoveButtons.None));
            Assert.That(SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents, Is.EqualTo(2));

            var blocker = Server.System<ActionBlockerSystem>();
            var probe = SEntMan.GetComponent<MovementMergeProbeComponent>(source);
            probe.BlockMovement = true;
            Assert.That(blocker.UpdateCanMove(source), Is.False);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(source), Is.False);
                Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(target), Is.False);
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(target).HeldMoveButtons, Is.EqualTo(MoveButtons.None));
            });

            var blocker = Server.System<ActionBlockerSystem>();
            SEntMan.GetComponent<MovementMergeProbeComponent>(source).BlockMovement = false;
            Assert.That(blocker.UpdateCanMove(source), Is.True);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(source), Is.True);
                Assert.That(SEntMan.GetComponent<ActiveInputMoverComponent>(target).RelayedFrom, Is.EqualTo(source));
            });

            var mobState = SEntMan.GetComponent<MobStateComponent>(source);
            Server.System<MobStateSystem>().ChangeMobState(source, MobState.Critical, mobState);
            SEntMan.GetComponent<InputMoverComponent>(source).CanMove = true;
        });

        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Down);
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(source).HeldMoveButtons, Is.EqualTo(MoveButtons.None));
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(target).HeldMoveButtons, Is.EqualTo(MoveButtons.None));
                Assert.That(SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents, Is.EqualTo(2),
                    "an incapacitated relay source must not forward movement");
            });

            Server.System<MobStateSystem>().ChangeMobState(source, MobState.Alive);
            SEntMan.GetComponent<InputMoverComponent>(source).CanMove = true;
        });
        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Up);
        await Pair.RunTicksSync(2);

        var targetEventsBeforeMissing = 0;
        await Server.WaitAssertion(() =>
        {
            var moverController = Server.System<MoverController>();
            Assert.That(SEntMan.RemoveComponent<InputMoverComponent>(target), Is.True);

            var sourceMover = SEntMan.GetComponent<InputMoverComponent>(source);
            moverController.SetVelocityDirection((source, sourceMover), Direction.East, ushort.MaxValue, true);
            Assert.That(sourceMover.HeldMoveButtons, Is.EqualTo(MoveButtons.Right));
            targetEventsBeforeMissing = SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents;
        });

        await SetKey(EngineKeyFunctions.MoveUp, BoundKeyState.Down);
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<InputMoverComponent>(target), Is.False,
                    "the explicit missing-mover probe must observe the removed target component");
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(source).HeldMoveButtons, Is.EqualTo(MoveButtons.None),
                    "a missing relay mover still clears stale input on its source");
                Assert.That(SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents,
                    Is.EqualTo(targetEventsBeforeMissing));
            });

            SEntMan.EnsureComponent<InputMoverComponent>(target);
        });
        await Pair.RunTicksSync(1);
        await SetKey(EngineKeyFunctions.MoveUp, BoundKeyState.Up);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var moverController = Server.System<MoverController>();
            var targetMover = SEntMan.GetComponent<InputMoverComponent>(target);
            moverController.SetVelocityDirection((target, targetMover), Direction.East, ushort.MaxValue, true);
            var eventsBeforeStaleCleanup = SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents;

            Assert.That(SEntMan.RemoveComponent<MovementRelayTargetComponent>(target), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(targetMover.HeldMoveButtons, Is.EqualTo(MoveButtons.None));
                Assert.That(SEntMan.GetComponent<MovementMergeProbeComponent>(target).MoveInputEvents,
                    Is.EqualTo(eventsBeforeStaleCleanup + 1),
                    "removing a stale relay target clears its held input exactly once");
            });
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var moverController = Server.System<MoverController>();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(source), Is.False);
                Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(target), Is.False);
                Assert.That(moverController.GetEffectiveMover(source), Is.EqualTo(source));
                Assert.That(SEntMan.GetComponent<MovementMergeProbeComponent>(source).EffectiveMoverChanges,
                    Is.EqualTo(2));
                Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(target), Is.True);
                Assert.That(SEntMan.GetComponent<ActiveInputMoverComponent>(target).RelayedFrom, Is.Null);
            });
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.TileFrictionModifier), 1f)]
    public async Task FrictionMultiplierPreservesPhysicalBaselinesAcrossContextChanges()
    {
        EntityUid defaultFriction = default;
        EntityUid doubleFriction = default;
        EntityUid zeroFriction = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<MovementMergeProbeSystem>();
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            defaultFriction = SEntMan.SpawnEntity("MovementMergeFrictionDefault", coordinates);
            doubleFriction = SEntMan.SpawnEntity("MovementMergeFrictionDouble", coordinates);
            zeroFriction = SEntMan.SpawnEntity("MovementMergeFrictionZero", coordinates);

            AssertFriction(defaultFriction, 2.5f, 2.5f);
            AssertFriction(doubleFriction, 5f, 5f);
            AssertFriction(zeroFriction, 0f, 0f);

            var standing = Server.System<StandingStateSystem>();
            Assert.That(standing.Down(defaultFriction, playSound: false, dropHeldItems: false, force: true), Is.True);
            Assert.That(standing.Down(doubleFriction, playSound: false, dropHeldItems: false, force: true), Is.True);
            Assert.That(standing.Down(zeroFriction, playSound: false, dropHeldItems: false, force: true), Is.True);

            AssertFriction(defaultFriction, 1f, 1f);
            AssertFriction(doubleFriction, 2f, 2f);
            AssertFriction(zeroFriction, 0f, 0f);

            Assert.That(standing.Stand(defaultFriction, force: true), Is.True);
            Assert.That(standing.Stand(doubleFriction, force: true), Is.True);
            Assert.That(standing.Stand(zeroFriction, force: true), Is.True);

            AssertFriction(defaultFriction, 2.5f, 2.5f);
            AssertFriction(doubleFriction, 5f, 5f);
            AssertFriction(zeroFriction, 0f, 0f);

            var probe = SEntMan.EnsureComponent<MovementMergeProbeComponent>(doubleFriction);
            probe.FrictionModifier = 0.5f;
            probe.AccelerationModifier = 0.25f;
            Server.System<MovementSpeedModifierSystem>().RefreshFrictionModifiers(doubleFriction);
            AssertFriction(doubleFriction, 2.5f, 2.5f);
            Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(doubleFriction).Acceleration,
                Is.EqualTo(MovementSpeedModifierComponent.DefaultAcceleration * 0.25f).Within(0.001f));

            Assert.That(standing.Down(doubleFriction, playSound: false, dropHeldItems: false, force: true), Is.True);
            AssertFriction(doubleFriction, 1f, 1f);
        });
    }

    [Test]
    public async Task RmcRestWaterAndPullUseMergedMovementSeams()
    {
        EntityUid activePullTarget = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<MovementMergeProbeSystem>();
            var speedSystem = Server.System<MovementSpeedModifierSystem>();
            var restingSystem = Server.System<RMCStandingSystem>();
            var source = SPlayer;
            var probe = SEntMan.EnsureComponent<MovementMergeProbeComponent>(source);
            probe.WalkSpeedModifier = 0.5f;
            probe.SprintSpeedModifier = 0.25f;
            var rest = SEntMan.EnsureComponent<RMCRestComponent>(source);

            restingSystem.SetRest((source, rest), true);
            speedSystem.RefreshMovementSpeedModifiers(source);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(source);
            Assert.Multiple(() =>
            {
                Assert.That(speed.WalkSpeedModifier, Is.EqualTo(rest.RestingSpeed).Within(0.001f));
                Assert.That(speed.SprintSpeedModifier, Is.EqualTo(rest.RestingSpeed).Within(0.001f));
                Assert.That(speed.CurrentWalkSpeed,
                    Is.EqualTo(speed.BaseWalkSpeed * rest.RestingSpeed).Within(0.001f));
                Assert.That(speed.CurrentSprintSpeed,
                    Is.EqualTo(speed.BaseSprintSpeed * rest.RestingSpeed).Within(0.001f));
            });

            restingSystem.SetRest((source, rest), false);
            speedSystem.RefreshMovementSpeedModifiers(source);
            Assert.Multiple(() =>
            {
                Assert.That(speed.WalkSpeedModifier, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(speed.SprintSpeedModifier, Is.EqualTo(0.25f).Within(0.001f));
            });

            var water = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.EnsureComponent<RMCWaterComponent>(water);
            var dryEntity = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var waterSystem = Server.System<RMCWaterSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(waterSystem.IsActiveWater(water, source), Is.True,
                    "uncovered RMC water remains active");
                Assert.That(waterSystem.IsActiveWater(dryEntity, source), Is.False);
            });

            activePullTarget = SEntMan.SpawnEntity(
                "CMMobHuman",
                SEntMan.GetComponent<TransformComponent>(source).Coordinates.Offset(new Vector2(0.5f, 0)));
        });

        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var source = SPlayer;
            var pullingSystem = Server.System<PullingSystem>();
            Assert.That(pullingSystem.TryStartPull(source, activePullTarget), Is.True);
            var activePuller = SEntMan.GetComponent<PullerComponent>(source);
            var activePullable = SEntMan.GetComponent<PullableComponent>(activePullTarget);
            var initialJoint = activePullable.PullJointId;
            Assert.Multiple(() =>
            {
                Assert.That(initialJoint, Is.Not.Null);
                Assert.That(activePuller.Pulling, Is.EqualTo(activePullTarget));
                Assert.That(activePullable.Puller, Is.EqualTo(source));
            });

            Assert.That(pullingSystem.TryDetachPullJointForTransfer(
                source,
                activePullTarget,
                activePuller,
                activePullable), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(activePullable.PullJointId, Is.Null);
                Assert.That(activePuller.Pulling, Is.EqualTo(activePullTarget),
                    "detaching the physical joint must preserve the logical pull relationship");
                Assert.That(activePullable.Puller, Is.EqualTo(source));
            });

            Assert.That(pullingSystem.TryRefreshPullJointForTransfer(
                source,
                activePullTarget,
                activePuller,
                activePullable), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(activePullable.PullJointId, Is.EqualTo(initialJoint),
                    "refresh recreates the deterministic pull joint after transfer");
                Assert.That(activePuller.Pulling, Is.EqualTo(activePullTarget));
                Assert.That(activePullable.Puller, Is.EqualTo(source));
            });

            var pullSource = SEntMan.SpawnEntity("MovementMergePullerSource", MapCoordinates.Nullspace);
            var pullTarget = SEntMan.SpawnEntity("MovementMergePullerTarget", MapCoordinates.Nullspace);
            var sourcePuller = SEntMan.GetComponent<PullerComponent>(pullSource);
            pullingSystem.CopyPullerComponent((pullSource, sourcePuller), pullTarget);
            var targetPuller = SEntMan.GetComponent<PullerComponent>(pullTarget);
            Assert.Multiple(() =>
            {
                Assert.That(targetPuller.NeedsHands, Is.False);
                Assert.That(targetPuller.ThrowCooldown, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(targetPuller.PullingAlert, Is.EqualTo(sourcePuller.PullingAlert));
                Assert.That(targetPuller.Pulling, Is.Null,
                    "copying compatibility fields must not transfer an active pull relationship");
            });
        });
    }

    [Test]
    public async Task AnchoredRelaySourceForwardsMovementToMovableTarget()
    {
        var source = SPlayer;
        EntityUid target = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var transform = Server.System<SharedTransformSystem>();
                target = SEntMan.SpawnEntity(
                    "MovementMergeRelayTarget",
                    SEntMan.GetComponent<TransformComponent>(source).Coordinates);

                transform.AnchorEntity(source);
                Server.System<MoverController>().SetRelay(source, target);
            });

            await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Down);
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(target).HeldMoveButtons,
                    Is.EqualTo(MoveButtons.Right),
                    "an anchored controller must still forward movement to its movable relay target");
            });
        }
        finally
        {
            await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Up);
            await Server.WaitPost(() =>
            {
                Server.System<SharedTransformSystem>().Unanchor(source);
                if (SEntMan.EntityExists(target))
                    SEntMan.DeleteEntity(target);
            });
        }
    }

    [Test]
    public async Task TileMovementUsesVirtualGroundClampsSpeedAndAccumulatesSubticks()
    {
        var source = SPlayer;
        var startingPosition = Vector2.Zero;
        var destination = Vector2.Zero;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<MovementMergeProbeSystem>();
            var moverController = Server.System<MoverController>();
            var probe = SEntMan.EnsureComponent<MovementMergeProbeComponent>(source);
            probe.VirtualGround = true;

            var virtualGround = new IsVirtualGroundForMovementEvent();
            SEntMan.EventBus.RaiseLocalEvent(source, ref virtualGround);
            Assert.That(virtualGround.Grounded, Is.True,
                "the by-ref virtual-ground contract must propagate the subscriber decision");

            var input = SEntMan.GetComponent<InputMoverComponent>(source);
            var physics = SEntMan.GetComponent<PhysicsComponent>(source);
            var transform = SEntMan.GetComponent<TransformComponent>(source);
            var tileMovement = SEntMan.EnsureComponent<CMUTileMovementComponent>(source);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(source);
            input.HeldMoveButtons = MoveButtons.Right;
            input.RelativeRotation = Angle.Zero;
            input.TargetRelativeRotation = Angle.Zero;
            startingPosition = transform.LocalPosition;

            Assert.That(moverController.HandleTileMovement(
                source,
                source,
                tileMovement,
                physics,
                transform,
                input,
                null,
                null,
                1f / 60f), Is.True);

            destination = SharedMoverController.SnapCoordinatesToTile(startingPosition + Vector2.UnitX);
            Assert.Multiple(() =>
            {
                Assert.That(tileMovement.SlideActive, Is.True);
                Assert.That(tileMovement.Destination.X, Is.EqualTo(destination.X).Within(0.001f));
                Assert.That(tileMovement.Destination.Y, Is.EqualTo(destination.Y).Within(0.001f));
                Assert.That(physics.LinearVelocity.X, Is.GreaterThan(0));
                Assert.That(physics.LinearVelocity.Length(), Is.EqualTo(speed.CurrentSprintSpeed).Within(0.001f),
                    "tile velocity is clamped to the current movement speed");
            });

            var subtickEntity = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetComponent<TransformComponent>(source).Coordinates);
            var subtickMover = SEntMan.GetComponent<InputMoverComponent>(subtickEntity);
            var quarterTick = (ushort) (ushort.MaxValue / 4);
            var threeQuarterTick = (ushort) (ushort.MaxValue * 3 / 4);
            moverController.SetVelocityDirection((subtickEntity, subtickMover), Direction.East, quarterTick, true);
            moverController.SetVelocityDirection((subtickEntity, subtickMover), Direction.East, threeQuarterTick, false);

            Assert.Multiple(() =>
            {
                Assert.That(subtickMover.CurTickSprintMovement.X, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(subtickMover.CurTickSprintMovement.Y, Is.EqualTo(0).Within(0.001f));
                Assert.That(subtickMover.LastInputSubTick, Is.EqualTo(threeQuarterTick));
                Assert.That(subtickMover.HeldMoveButtons, Is.EqualTo(MoveButtons.None));
            });
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var current = SEntMan.GetComponent<TransformComponent>(source).LocalPosition;
            Assert.That(current.X, Is.GreaterThan(startingPosition.X),
                "the physics-backed tile slide must move toward its snapped destination");
            Assert.That(current.X, Is.LessThanOrEqualTo(destination.X + 0.05f));
        });
    }

    private void AssertFriction(EntityUid uid, float friction, float frictionNoInput)
    {
        var component = SEntMan.GetComponent<MovementSpeedModifierComponent>(uid);
        Assert.Multiple(() =>
        {
            Assert.That(component.Friction, Is.EqualTo(friction).Within(0.001f));
            Assert.That(component.FrictionNoInput, Is.EqualTo(frictionNoInput).Within(0.001f));
        });
    }
}

[RegisterComponent]
public sealed partial class MovementMergeProbeComponent : Component
{
    public int MoveInputEvents;
    public int EffectiveMoverChanges;
    public bool BlockMovement;
    public bool VirtualGround;
    public float WalkSpeedModifier = 1;
    public float SprintSpeedModifier = 1;
    public float FrictionModifier = 1;
    public float AccelerationModifier = 1;
}

public sealed class MovementMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MovementMergeProbeComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<MovementMergeProbeComponent, EffectiveMoverChangedEvent>(OnEffectiveMoverChanged);
        SubscribeLocalEvent<MovementMergeProbeComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<MovementMergeProbeComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<MovementMergeProbeComponent, RefreshFrictionModifiersEvent>(OnRefreshFriction);
        SubscribeLocalEvent<MovementMergeProbeComponent, IsVirtualGroundForMovementEvent>(OnVirtualGround);
    }

    private static void OnMoveInput(Entity<MovementMergeProbeComponent> ent, ref MoveInputEvent args)
    {
        ent.Comp.MoveInputEvents++;
    }

    private static void OnEffectiveMoverChanged(
        Entity<MovementMergeProbeComponent> ent,
        ref EffectiveMoverChangedEvent args)
    {
        ent.Comp.EffectiveMoverChanges++;
    }

    private static void OnUpdateCanMove(Entity<MovementMergeProbeComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.BlockMovement)
            args.Cancel();
    }

    private static void OnRefreshMovementSpeed(
        Entity<MovementMergeProbeComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.WalkSpeedModifier, ent.Comp.SprintSpeedModifier);
    }

    private static void OnRefreshFriction(
        Entity<MovementMergeProbeComponent> ent,
        ref RefreshFrictionModifiersEvent args)
    {
        args.ModifyFriction(ent.Comp.FrictionModifier);
        args.ModifyAcceleration(ent.Comp.AccelerationModifier);
    }

    private static void OnVirtualGround(
        Entity<MovementMergeProbeComponent> ent,
        ref IsVirtualGroundForMovementEvent args)
    {
        args.Grounded |= ent.Comp.VirtualGround;
    }
}
