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

    [Test]
    public async Task RotateFalseStillGetsOneAuthoritativeLungeAndObserverPopup()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid user = default;
        EntityUid observer = default;
        EntityUid item = default;
        EntityUid rotateItem = default;
        EntityUid popupItem = default;
        NetEntity userNet = default;
        NetEntity itemNet = default;
        var popupBefore = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                observer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                item = SEntMan.SpawnEntity("ThrowMergeItem", map.GridCoords);
                rotateItem = SEntMan.SpawnEntity("ThrowMergeItem", map.GridCoords);
                userNet = SEntMan.GetNetEntity(user);
                itemNet = SEntMan.GetNetEntity(item);
                Server.System<SharedTransformSystem>().SetLocalRotation(user, Angle.Zero);
                Server.PlayerMan.SetAttachedEntity(session, user);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var probe = Client.System<ThrowMergeProbeSystem>();
                probe.Reset();
                var clientUser = CEntMan.GetEntity(userNet);
                var clientItem = CEntMan.GetEntity(itemNet);
                Assert.That(Client.System<ThrowingSystem>().TryThrow(
                    clientItem,
                    Vector2.UnitY,
                    user: clientUser,
                    pushbackRatio: 0,
                    animated: false,
                    playSound: false,
                    doSpin: false,
                    rotate: false), Is.True);
                Assert.That(probe.Lunges, Is.Zero,
                    "the shared client prediction path must not create its own lunge");
            });

            await Server.WaitAssertion(() =>
            {
                var transform = Server.System<SharedTransformSystem>();
                var before = transform.GetWorldRotation(user);
                Assert.That(Server.System<ThrowingSystem>().TryThrow(
                    item,
                    Vector2.UnitY,
                    user: user,
                    pushbackRatio: 0,
                    animated: false,
                    playSound: true,
                    doSpin: false,
                    rotate: false), Is.True);
                Assert.That(transform.GetWorldRotation(user), Is.EqualTo(before),
                    "rotate:false suppresses facing without suppressing the authoritative recoil lunge");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<ThrowMergeProbeSystem>().Lunges, Is.EqualTo(1));
            });

            await Server.WaitAssertion(() =>
            {
                var transform = Server.System<SharedTransformSystem>();
                var before = transform.GetWorldRotation(user);
                Assert.That(Server.System<ThrowingSystem>().TryThrow(
                    rotateItem,
                    Vector2.UnitY,
                    user: user,
                    pushbackRatio: 0,
                    animated: false,
                    playSound: true,
                    doSpin: false,
                    rotate: true), Is.True);
                Assert.That(transform.GetWorldRotation(user), Is.Not.EqualTo(before));
                Server.PlayerMan.SetAttachedEntity(session, observer);
            });
            await Pair.RunTicksSync(3);

            var expectedPopup = string.Empty;
            await Server.WaitAssertion(() =>
            {
                popupItem = SEntMan.SpawnEntity("ThrowMergeItem", map.GridCoords);
                expectedPopup = Loc.GetString("throwing-user-threw-others",
                    ("user", Content.Shared.IdentityManagement.Identity.Name(user, SEntMan, observer)),
                    ("thrown", Content.Shared.IdentityManagement.Identity.Name(popupItem, SEntMan, observer)));
            });
            await Pair.RunTicksSync(1);
            await Client.WaitAssertion(() =>
            {
                popupBefore = PopupCount(expectedPopup, CEntMan.GetEntity(userNet));
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<ThrowingSystem>().TryThrow(
                    popupItem,
                    Vector2.UnitX,
                    user: user,
                    pushbackRatio: 0,
                    recoil: false,
                    animated: false,
                    playSound: false,
                    doSpin: false), Is.True);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Client.System<ThrowMergeProbeSystem>().Lunges, Is.EqualTo(2),
                        "two server throws produce exactly two client lunges despite one client prediction");
                    Assert.That(PopupCount(expectedPopup, CEntMan.GetEntity(userNet)), Is.EqualTo(popupBefore + 1),
                        "observer identity popup occurs before the pushbackRatio zero early return");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
            await Delete(user, observer, item, rotateItem, popupItem);
            await Pair.RunTicksSync(2);
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(uid);
        return uid;
    }

    private int PopupCount(string message, EntityUid entity)
    {
        var dictionary = (IDictionary) typeof(PopupSystem)
            .GetField("_aliveWorldLabels", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Client.System<PopupSystem>())!;
        var count = 0;
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key;
            var type = key.GetType();
            if ((string) type.GetProperty("Message")!.GetValue(key)! == message &&
                (EntityUid?) type.GetProperty("Entity")!.GetValue(key) == entity)
            {
                count++;
            }
        }

        return count;
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
