using System.Collections;
using System.Numerics;
using System.Reflection;
using Content.Client.Popups;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Construction.Components;
using Content.Shared.Gravity;
using Content.Shared.IdentityManagement;
using Content.Shared.Slippery;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
[TestOf(typeof(ThrowingSystem))]
public sealed class ThrowingMergeRegressionTest : GameTest
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
          parent: BaseItem
          id: ThrowMergeItem
          components:
          - type: ThrowMergeProbe

        - type: entity
          parent: BaseItem
          id: ThrowMergeUnanchorable
          components:
          - type: Anchorable
            flags:
            - Anchorable
            - Unanchorable

        - type: entity
          parent: BaseItem
          id: ThrowMergeFixedAnchor
          components:
          - type: Anchorable
            flags:
            - Anchorable
        """;

    [Test]
    public async Task InvalidInputsDoNotMutateAndUnanchorStrengthHonorsFlags()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var throwing = Server.System<ThrowingSystem>();
                var transform = Server.System<SharedTransformSystem>();
                var invalid = Spawn("ThrowMergeUnanchorable", map.GridCoords, entities);
                Assert.That(transform.AnchorEntity(invalid), Is.True);
                var physics = SEntMan.GetComponent<PhysicsComponent>(invalid);
                var initialVelocity = physics.LinearVelocity;

                Assert.Multiple(() =>
                {
                    Assert.That(throwing.TryThrow(invalid, new Vector2(float.NaN, 1), unanchor: ThrowingUnanchorStrength.All), Is.False);
                    Assert.That(throwing.TryThrow(invalid, Vector2.One, baseThrowSpeed: float.PositiveInfinity, unanchor: ThrowingUnanchorStrength.All), Is.False);
                    Assert.That(throwing.TryThrow(invalid, Vector2.One, baseThrowSpeed: -1, unanchor: ThrowingUnanchorStrength.All), Is.False);
                    Assert.That(throwing.TryThrow(invalid, Vector2.One, friction: -1, unanchor: ThrowingUnanchorStrength.All), Is.False);
                    Assert.That(SEntMan.HasComponent<ThrownItemComponent>(invalid), Is.False);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(invalid).Anchored, Is.True,
                        "validation must occur before even an All-strength unanchor request");
                    Assert.That(physics.LinearVelocity, Is.EqualTo(initialVelocity));
                });

                var nonFiniteFriction = Spawn("ThrowMergeItem", map.GridCoords, entities);
                Assert.That(throwing.TryThrow(
                    nonFiniteFriction,
                    Vector2.UnitX,
                    friction: float.NaN,
                    recoil: false,
                    animated: false,
                    playSound: false,
                    doSpin: false), Is.True,
                    "non-finite friction is normalized to the default rather than rejecting a valid throw");
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(nonFiniteFriction), Is.True);

                var none = Spawn("ThrowMergeUnanchorable", map.GridCoords, entities);
                var eligible = Spawn("ThrowMergeUnanchorable", map.GridCoords, entities);
                var fixedByFlags = Spawn("ThrowMergeFixedAnchor", map.GridCoords, entities);
                var forced = Spawn("ThrowMergeFixedAnchor", map.GridCoords, entities);
                Assert.That(transform.AnchorEntity(none), Is.True);
                Assert.That(transform.AnchorEntity(eligible), Is.True);
                Assert.That(transform.AnchorEntity(fixedByFlags), Is.True);
                Assert.That(transform.AnchorEntity(forced), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(throwing.TryThrow(none, Vector2.UnitX, unanchor: ThrowingUnanchorStrength.None), Is.False);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(none).Anchored, Is.True);
                    Assert.That(throwing.TryThrow(eligible, Vector2.UnitX, unanchor: ThrowingUnanchorStrength.Unanchorable), Is.True);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(eligible).Anchored, Is.False);
                    Assert.That(throwing.TryThrow(fixedByFlags, Vector2.UnitX, unanchor: ThrowingUnanchorStrength.Unanchorable), Is.False);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(fixedByFlags).Anchored, Is.True);
                    Assert.That(throwing.TryThrow(forced, Vector2.UnitX, unanchor: ThrowingUnanchorStrength.All), Is.True);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(forced).Anchored, Is.False);
                });
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    [Test]
    public async Task ImpulsePrecedesEventsAndCancellationGatesThrowerPushback()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var throwing = Server.System<ThrowingSystem>();
                var physics = Server.System<SharedPhysicsSystem>();
                var probeSystem = Server.System<ThrowMergeProbeSystem>();
                var user = Spawn("MobHuman", map.GridCoords, entities);
                var item = Spawn("ThrowMergeItem", map.GridCoords, entities);
                var userProbe = SEntMan.EnsureComponent<ThrowMergeProbeComponent>(user);
                userProbe.ForcePush = true;
                probeSystem.Reset();

                Assert.That(throwing.TryThrow(
                    item,
                    Vector2.UnitX,
                    user: user,
                    pushbackRatio: 1,
                    recoil: false,
                    animated: false,
                    playSound: false,
                    doSpin: false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(probeSystem.Order.Take(2), Is.EqualTo(new[] { "thrown", "thrower" }));
                    Assert.That(probeSystem.ThrownSawImpulse, Is.True,
                        "the thrown entity receives its impulse before either success event");
                    Assert.That(probeSystem.PushbackAttempts, Is.EqualTo(1));
                    Assert.That(probeSystem.ThrowerImpulses, Is.EqualTo(1));
                    Assert.That(SEntMan.GetComponent<PhysicsComponent>(user).LinearVelocity.LengthSquared(), Is.GreaterThan(0));
                });

                physics.SetLinearVelocity(user, Vector2.Zero);
                var cancelled = Spawn("ThrowMergeItem", map.GridCoords, entities);
                SEntMan.GetComponent<ThrowMergeProbeComponent>(cancelled).CancelPushback = true;
                probeSystem.Reset();
                Assert.That(throwing.TryThrow(
                    cancelled,
                    Vector2.UnitX,
                    user: user,
                    pushbackRatio: 1,
                    recoil: false,
                    animated: false,
                    playSound: false,
                    doSpin: false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(probeSystem.PushbackAttempts, Is.EqualTo(1));
                    Assert.That(probeSystem.ThrowerImpulses, Is.Zero);
                    Assert.That(SEntMan.GetComponent<PhysicsComponent>(user).LinearVelocity, Is.EqualTo(Vector2.Zero));
                });

                var gravityItem = Spawn("ThrowMergeItem", map.GridCoords, entities);
                var gravity = SEntMan.EnsureComponent<GravityAffectedComponent>(user);
                gravity.Weightless = true;
                userProbe.ForcePush = false;
                physics.SetLinearVelocity(user, Vector2.Zero);
                Assert.That(throwing.TryThrow(
                    gravityItem,
                    Vector2.UnitX,
                    user: user,
                    pushbackRatio: 1,
                    recoil: false,
                    animated: false,
                    playSound: false,
                    doSpin: false), Is.True);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(user).LinearVelocity.LengthSquared(), Is.GreaterThan(0),
                    "weightlessness opts the thrower into recoil impulse");

                SEntMan.RemoveComponent<GravityAffectedComponent>(user);
                SEntMan.EnsureComponent<SlidingComponent>(user);
                physics.SetLinearVelocity(user, Vector2.Zero);
                var slidingItem = Spawn("ThrowMergeItem", map.GridCoords, entities);
                Assert.That(throwing.TryThrow(
                    slidingItem,
                    Vector2.UnitX,
                    user: user,
                    pushbackRatio: 1,
                    recoil: false,
                    animated: false,
                    playSound: false,
                    doSpin: false), Is.True);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(user).LinearVelocity.LengthSquared(), Is.GreaterThan(0),
                    "SlidingComponent independently opts the thrower into recoil impulse");
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(uid);
        return uid;
    }

    private async Task Delete(params EntityUid[] entities)
    {
        foreach (var uid in entities)
        {
            if (SEntMan.EntityExists(uid))
                await Pair.DeleteEntityTreeLeafFirst(uid);
        }
    }
}

[RegisterComponent]
public sealed partial class ThrowMergeProbeComponent : Component
{
    public bool CancelPushback;
    public bool ForcePush;
}

public sealed class ThrowMergeProbeSystem : EntitySystem
{
    public readonly List<string> Order = new();
    public bool ThrownSawImpulse;
    public int PushbackAttempts;
    public int ThrowerImpulses;
    public int Lunges;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrowMergeProbeComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<ThrowMergeProbeComponent, ThrowEvent>(OnThrow);
        SubscribeLocalEvent<ThrowMergeProbeComponent, ThrowPushbackAttemptEvent>(OnPushbackAttempt);
        SubscribeLocalEvent<ThrowMergeProbeComponent, ThrowerImpulseEvent>(OnThrowerImpulse);
        SubscribeNetworkEvent<MeleeLungeEvent>(OnLunge);
    }

    public void Reset()
    {
        Order.Clear();
        ThrownSawImpulse = false;
        PushbackAttempts = 0;
        ThrowerImpulses = 0;
        Lunges = 0;
    }

    private void OnThrown(Entity<ThrowMergeProbeComponent> ent, ref ThrownEvent args)
    {
        Order.Add("thrown");
        ThrownSawImpulse = Comp<PhysicsComponent>(ent).LinearVelocity.LengthSquared() > 0;
    }

    private void OnThrow(Entity<ThrowMergeProbeComponent> ent, ref ThrowEvent args)
    {
        Order.Add("thrower");
    }

    private void OnPushbackAttempt(Entity<ThrowMergeProbeComponent> ent, ref ThrowPushbackAttemptEvent args)
    {
        PushbackAttempts++;
        if (ent.Comp.CancelPushback)
            args.Cancel();
    }

    private void OnThrowerImpulse(Entity<ThrowMergeProbeComponent> ent, ref ThrowerImpulseEvent args)
    {
        ThrowerImpulses++;
        args.Push |= ent.Comp.ForcePush;
    }

    private void OnLunge(MeleeLungeEvent args)
    {
        Lunges++;
    }
}
