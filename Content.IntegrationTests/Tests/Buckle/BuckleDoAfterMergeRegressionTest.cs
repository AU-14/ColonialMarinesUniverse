#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Buckle;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Buckle;

[TestFixture]
[TestOf(typeof(SharedBuckleSystem))]
[TestOf(typeof(SharedDoAfterSystem))]
public sealed class BuckleDoAfterMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: BuckleDoAfterMergeMobBase
          abstract: true
          components:
          - type: Buckle
            range: 3
            delay: 0
          - type: Hands
          - type: ComplexInteraction
          - type: InputMover
          - type: StandingState
          - type: RMCRest
          - type: RMCBuckleOffset
            offset: 0, 0.5
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              hard:
                shape:
                  !type:PhysShapeCircle
                  radius: 0.35
                mask:
                - MidImpassable
                - HighImpassable
                layer:
                - MobLayer
          - type: BuckleDoAfterMergeProbe

        - type: entity
          parent: BuckleDoAfterMergeMobBase
          id: BuckleDoAfterMergeAllowed
          components:
          - type: BuckleDoAfterMergeAllowed

        - type: entity
          parent: BuckleDoAfterMergeMobBase
          id: BuckleDoAfterMergeDenied

        - type: entity
          id: BuckleDoAfterMergeStrap
          components:
          - type: Strap
            position: Stand
            buckleOffset: 0.25, 0
            buckledAlertType: null
            whitelist:
              components:
              - BuckleDoAfterMergeAllowed
          - type: BuckleDoAfterMergeProbe

        - type: entity
          parent: BuckleDoAfterMergeStrap
          id: BuckleDoAfterMergeMovingStrap
          components:
          - type: RMCAllowStrapMovement

        - type: entity
          id: BuckleDoAfterMergeWall
          components:
          - type: Tag
            tags:
            - Wall
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              hard:
                shape:
                  !type:PhysShapeCircle
                  radius: 0.35
                mask:
                - MobMask
                layer:
                - WallLayer

        - type: entity
          id: BuckleDoAfterMergeUser
          components:
          - type: DoAfter
          - type: Hands

        - type: entity
          id: BuckleDoAfterMergeRangefinder
          components:
          - type: Rangefinder
            range: 1

        - type: entity
          id: BuckleDoAfterMergeTargetEffect
          components:
          - type: BuckleDoAfterMergeTargetEffect
        """;

    [Test]
    public async Task BuckleRetainsRmcOffsetMovementCollisionAndRestContracts()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();
        EntityUid target = default;
        EntityUid denied = default;
        EntityUid strap = default;
        EntityUid shutter = default;
        EntityUid xeno = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var buckle = Server.System<SharedBuckleSystem>();
                var targetCoords = map.GridCoords;
                var strapCoords = map.GridCoords.Offset(new Vector2(2, 0));
                target = Spawn("BuckleDoAfterMergeAllowed", targetCoords, entities);
                denied = Spawn("BuckleDoAfterMergeDenied", targetCoords, entities);
                strap = Spawn("BuckleDoAfterMergeStrap", strapCoords, entities);
                xeno = Spawn("CMXenoDrone", targetCoords, entities);
                SEntMan.EnsureComponent<HandsComponent>(xeno);

                var strapComp = SEntMan.GetComponent<StrapComponent>(strap);
                Assert.That(strapComp.BuckledAlertType, Is.Null,
                    "RMC weapon mounts may deliberately suppress the upstream buckled alert");

                buckle.StrapSetEnabled(strap, false, strapComp);
                Assert.That(buckle.TryBuckle(target, target, strap, popup: false), Is.False);
                Assert.That(strapComp.BuckledEntities, Is.Empty);

                buckle.StrapSetEnabled(strap, true, strapComp);
                Assert.That(buckle.TryBuckle(denied, denied, strap, popup: false), Is.False,
                    "the strap whitelist remains authoritative after the Enabled gate");
                Assert.That(buckle.TryBuckle(target, xeno, strap, popup: false), Is.False,
                    "xenos cannot operate ordinary straps even when they otherwise have hands");

                shutter = Spawn("RMCShutterAlmayer", map.GridCoords.Offset(new Vector2(1, 0)), entities);
                Assert.That(buckle.TryBuckle(target, target, strap, popup: false), Is.False,
                    "a closed RMC shutter between the target and strap must fail CanClimbOver before buckling");
                Assert.That(strapComp.BuckledEntities, Is.Empty);
            });

            await Delete(shutter);

            await Server.WaitAssertion(() =>
            {
                var actionBlocker = Server.System<ActionBlockerSystem>();
                var buckle = Server.System<SharedBuckleSystem>();
                var standing = Server.System<StandingStateSystem>();
                var transform = Server.System<SharedTransformSystem>();
                var probe = Server.System<BuckleDoAfterMergeProbeSystem>();
                probe.Reset();

                Assert.That(buckle.TryBuckle(target, target, strap, popup: false), Is.True);
                var targetBuckle = SEntMan.GetComponent<BuckleComponent>(target);
                var targetXform = SEntMan.GetComponent<TransformComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That(targetBuckle.BuckledTo, Is.EqualTo(strap));
                    Assert.That((targetXform.LocalPosition - new Vector2(0.25f, 0.5f)).Length(),
                        Is.LessThan(0.0001f),
                        "the strap offset and RMC species offset must both survive the transform check");
                    Assert.That(actionBlocker.CanMove(target), Is.False,
                        "ordinary straps retain upstream movement blocking");
                    Assert.That(probe.Order, Is.EqualTo(new[] { "strapped", "buckled" }));
                });

                // The structured collision event only needs a Wall-tagged fixture. Keep its real physics body
                // away from the target so it does not obstruct the subsequent unbuckle accessibility ray.
                var wall = Spawn("BuckleDoAfterMergeWall", map.GridCoords.Offset(new Vector2(10, 0)), entities);
                var targetPhysics = SEntMan.GetComponent<PhysicsComponent>(target);
                var targetFixture = SEntMan.GetComponent<FixturesComponent>(target).Fixtures["hard"];
                var wallPhysics = SEntMan.GetComponent<PhysicsComponent>(wall);
                var wallFixture = SEntMan.GetComponent<FixturesComponent>(wall).Fixtures["hard"];
                var collide = new PreventCollideEvent(target, wall, targetPhysics, wallPhysics, targetFixture, wallFixture);
                SEntMan.EventBus.RaiseLocalEvent(target, ref collide);
                Assert.That(collide.Cancelled, Is.True,
                    "a buckled entity must not collide with Wall-tagged vehicle interior boundaries");

                var beforeUnbuckle = transform.GetMapCoordinates(target);
                var rest = SEntMan.GetComponent<RMCRestComponent>(target);
                rest.Resting = true;
                Assert.That(buckle.TryUnbuckle(target, target, popup: false), Is.True);
                var afterUnbuckle = transform.GetMapCoordinates(target);
                var fixtures = SEntMan.GetComponent<FixturesComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That((afterUnbuckle.Position - beforeUnbuckle.Position).Length(), Is.LessThan(0.0001f),
                        "offset buckles restore the exact pre-unbuckle mover coordinates after PlaceNextTo");
                    Assert.That(standing.IsDown(target), Is.True,
                        "an RMC-resting mob remains down after unbuckling");
                    Assert.That(fixtures.Fixtures["hard"].CollisionMask & (int) CollisionGroup.MidImpassable, Is.Zero,
                        "resting unbuckle uses the explicit collision-changing down transition");
                    Assert.That(probe.Order,
                        Is.EqualTo(new[] { "strapped", "buckled", "unbuckled", "unstrapped" }),
                        "target and strap broadcast events retain their established ordering");
                });

                rest.Resting = false;
                Assert.That(standing.Stand(target, force: true), Is.True);
                var movingStrap = Spawn("BuckleDoAfterMergeMovingStrap", afterUnbuckle, entities);
                Assert.That(buckle.TryBuckle(target, target, movingStrap, popup: false), Is.True);
                Assert.That(actionBlocker.CanMove(target), Is.True,
                    "RMCAllowStrapMovement is the explicit exception to ordinary strap immobilization");
                Assert.That(buckle.TryUnbuckle(target, target, popup: false), Is.True);
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    [Test]
    public async Task DoAfterEffectsRangeRestAndDesignatorChecksRemainComposable()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();
        EntityUid user = default;
        EntityUid target = default;
        EntityUid farTarget = default;
        EntityUid rangefinder = default;
        DoAfterId? cadenceId = null;
        var timing = Server.ResolveDependency<IGameTiming>();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        var secondTicks = (int) Math.Ceiling(TimeSpan.FromSeconds(1).TotalSeconds / timing.TickPeriod.TotalSeconds);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var doAfter = Server.System<SharedDoAfterSystem>();
                user = Spawn("BuckleDoAfterMergeUser", map.GridCoords, entities);
                target = Spawn("BuckleDoAfterMergeUser", map.GridCoords, entities);
                farTarget = Spawn("BuckleDoAfterMergeUser", map.GridCoords.Offset(new Vector2(20, 0)), entities);
                rangefinder = Spawn("BuckleDoAfterMergeRangefinder", map.GridCoords, entities);
                Server.PlayerMan.SetAttachedEntity(session, user);

                var cadence = new BuckleDoAfterMergeEvent();
                var args = new DoAfterArgs(SEntMan, user, TimeSpan.FromSeconds(10), cadence, null, target)
                {
                    Broadcast = true,
                    BlockDuplicate = false,
                    BreakOnRest = false,
                    RequireCanInteract = false,
                    TargetEffect = "BuckleDoAfterMergeTargetEffect",
                };
                Assert.That(doAfter.TryStartDoAfter(args, out cadenceId), Is.True);
            });

            await Pair.RunTicksSync(secondTicks + 2);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityQuery<BuckleDoAfterMergeTargetEffectComponent>().Count(), Is.EqualTo(2),
                    "the authoritative do-after spawns its target effect immediately and then once per second");
                Server.System<SharedDoAfterSystem>().Cancel(cadenceId);
            });
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.EntityQuery<BuckleDoAfterMergeTargetEffectComponent>().Count(), Is.EqualTo(2),
                    "client prediction must not create duplicate local target effects");
            });

            DoAfterId? missingTargetId = null;
            await Server.WaitAssertion(() =>
            {
                var missing = new DoAfterArgs(
                    SEntMan,
                    user,
                    TimeSpan.FromSeconds(10),
                    new BuckleDoAfterMergeEvent(),
                    null)
                {
                    Broadcast = true,
                    BlockDuplicate = false,
                    BreakOnRest = false,
                    RequireCanInteract = false,
                    TargetEffect = "BuckleDoAfterMergeTargetEffect",
                };
                Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(missing, out missingTargetId), Is.True);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityQuery<BuckleDoAfterMergeTargetEffectComponent>().Count(), Is.EqualTo(2),
                    "a missing target transform must not spawn the configured effect");
                Server.System<SharedDoAfterSystem>().Cancel(missingTargetId);
            });

            var bypass = new BuckleDoAfterMergeEvent();
            var checkedRange = new BuckleDoAfterMergeEvent();
            await Server.WaitAssertion(() =>
            {
                var doAfter = Server.System<SharedDoAfterSystem>();
                var bypassArgs = new DoAfterArgs(SEntMan, user, timing.TickPeriod * 2, bypass, null, farTarget)
                {
                    Broadcast = true,
                    BlockDuplicate = false,
                    CancelDuplicate = false,
                    BreakOnRest = false,
                    BreakOnMove = false,
                    RequireCanInteract = false,
                    RangeCheck = false,
                };
                var checkedArgs = new DoAfterArgs(SEntMan, user, timing.TickPeriod * 2, checkedRange, null, farTarget)
                {
                    Broadcast = true,
                    BlockDuplicate = false,
                    CancelDuplicate = false,
                    BreakOnRest = false,
                    BreakOnMove = false,
                    RequireCanInteract = false,
                    RangeCheck = true,
                };
                Assert.That(doAfter.TryStartDoAfter(bypassArgs), Is.True);
                Assert.That(doAfter.TryStartDoAfter(checkedArgs), Is.False);
            });
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(bypass.DoAfter.Completed, Is.True,
                        "RangeCheck=false is the deliberate bypass for remote RMC actions");
                    Assert.That(bypass.Cancelled, Is.False);
                });
            });

            var resting = new BuckleDoAfterMergeEvent();
            await Server.WaitAssertion(() =>
            {
                var args = new DoAfterArgs(SEntMan, user, TimeSpan.FromSeconds(10), resting, null)
                {
                    Broadcast = true,
                    BlockDuplicate = false,
                    BreakOnRest = true,
                    RequireCanInteract = false,
                };
                Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(args), Is.True);
                SEntMan.EnsureComponent<XenoRestingComponent>(user);
            });
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                Assert.That(resting.Cancelled, Is.True);
                SEntMan.RemoveComponent<XenoRestingComponent>(user);
            });

            var designator = new LaserDesignatorDoAfterEvent(
                SEntMan.GetNetCoordinates(map.GridCoords.Offset(new Vector2(20, 0))));
            await Server.WaitAssertion(() =>
            {
                var args = new DoAfterArgs(SEntMan, user, TimeSpan.FromSeconds(10), designator, rangefinder)
                {
                    Broadcast = false,
                    BlockDuplicate = false,
                    BreakOnRest = false,
                    RequireCanInteract = false,
                    RangeCheck = false,
                };
                Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(args), Is.True);
            });
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                Assert.That(designator.Cancelled, Is.True,
                    "the RMC laser-designator coordinate check remains independent of generic RangeCheck");
            });
        }
        finally
        {
            var cleanup = entities.ToList();
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                cleanup.AddRange(
                    SEntMan.EntityQuery<BuckleDoAfterMergeTargetEffectComponent>().Select(component => component.Owner));
            });
            await Delete(cleanup.Distinct().ToArray());
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(uid);
        return uid;
    }

    private EntityUid Spawn(string prototype, MapCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(uid);
        return uid;
    }

    private async Task Delete(params EntityUid[] entities)
    {
        await Server.WaitPost(() =>
        {
            foreach (var uid in entities)
            {
                if (SEntMan.EntityExists(uid))
                    SEntMan.DeleteEntity(uid);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class BuckleDoAfterMergeAllowedComponent : Component;

[RegisterComponent]
public sealed partial class BuckleDoAfterMergeProbeComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class BuckleDoAfterMergeTargetEffectComponent : Component;

[Serializable, NetSerializable]
public sealed partial class BuckleDoAfterMergeEvent : SimpleDoAfterEvent;

public sealed partial class BuckleDoAfterMergeProbeSystem : EntitySystem
{
    public readonly List<string> Order = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BuckleDoAfterMergeProbeComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<BuckleDoAfterMergeProbeComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<BuckleDoAfterMergeProbeComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<BuckleDoAfterMergeProbeComponent, UnstrappedEvent>(OnUnstrapped);
    }

    public void Reset()
    {
        Order.Clear();
    }

    private void OnStrapped(Entity<BuckleDoAfterMergeProbeComponent> ent, ref StrappedEvent args)
    {
        Order.Add("strapped");
    }

    private void OnBuckled(Entity<BuckleDoAfterMergeProbeComponent> ent, ref BuckledEvent args)
    {
        Order.Add("buckled");
    }

    private void OnUnbuckled(Entity<BuckleDoAfterMergeProbeComponent> ent, ref UnbuckledEvent args)
    {
        Order.Add("unbuckled");
    }

    private void OnUnstrapped(Entity<BuckleDoAfterMergeProbeComponent> ent, ref UnstrappedEvent args)
    {
        Order.Add("unstrapped");
    }
}

#pragma warning restore RA0002
