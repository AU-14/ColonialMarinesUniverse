#pragma warning disable RA0002 // Regression setup drives authoritative light state and timer boundaries.

using Content.IntegrationTests.Fixtures;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Doom;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Light;

[TestFixture]
[TestOf(typeof(ExpendableLightSystem))]
public sealed class ExpendableLightReplicationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMFlare
          id: CMUExpendableLightReplicationProbe
          components:
          - type: ExpendableLight
            glowDuration: 120
            fadeOutDuration: 60
            refuelMaterialID: WoodPlank
            refuelMaterialTime: 15
            refuelMaximumDuration: 300
            litSound: null
            loopedSound: null
            dieSound: null
          - type: IgnitionSource
        """;

    [Test]
    public async Task BurnCountdownDoesNotDirtyUntilStateTransitionsReachClient()
    {
        var map = await Pair.CreateTestMap();
        Entity<ExpendableLightComponent> flare = default;
        NetEntity net = default;
        GameTick litDirty = default;
        float initialTime = 0;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, map.Grid.Owner);
            flare = SpawnFlare(map.GridCoords);
            net = SEntMan.GetNetEntity(flare.Owner);
            Assert.That(Server.System<ExpendableLightSystem>().TryActivate(flare), Is.True);
            litDirty = flare.Comp.LastModifiedTick;
            initialTime = flare.Comp.StateExpiryTime;
        });
        await Pair.RunTicksSync(10);
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
            Assert.That(flare.Comp.StateExpiryTime, Is.LessThan(initialTime));
            Assert.That(flare.Comp.LastModifiedTick, Is.EqualTo(litDirty),
                "a changing server countdown must not dirty the replicated light every tick");
        });
        await AssertClientState(net, ExpendableLightState.Lit, "turn_on");

        await Server.WaitAssertion(() =>
        {
            Server.System<SharedPhysicsSystem>().SetBodyType(flare.Owner, BodyType.Static);
            Server.System<ExpendableLightSystem>().Update(flare.Comp.StateExpiryTime + 0.25f);
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Fading));
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(60f),
                "the existing transition starts the full fade interval without carrying excess frame time");
            Assert.That(flare.Comp.LastModifiedTick, Is.GreaterThan(litDirty));
            Assert.That(SEntMan.GetComponent<IgnitionSourceComponent>(flare.Owner).Ignited, Is.True);
            Assert.That(SEntMan.GetComponent<ItemComponent>(flare.Owner).HeldPrefix, Is.EqualTo("lit"));
        });
        await Pair.RunUntilSynced();
        await AssertClientState(net, ExpendableLightState.Fading, "fade_out");

        await Server.WaitAssertion(() =>
        {
            Server.System<ExpendableLightSystem>().Update(flare.Comp.StateExpiryTime);
            AssertSpent(flare);
        });
        await Pair.RunUntilSynced();
        await AssertClientState(net, ExpendableLightState.Dead, string.Empty);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<Robust.Client.GameObjects.PointLightComponent>(CEntMan.GetEntity(net)).Enabled,
                Is.False);
        });
    }

    [Test]
    public async Task RefuelingReplicatesRevivalAndPreservesBurnTimeAndFuelLimit()
    {
        var map = await Pair.CreateTestMap();
        Entity<ExpendableLightComponent> flare = default;
        EntityUid fuel = default;
        NetEntity net = default;
        GameTick deadDirty = default;
        int initialFuel = 0;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, map.Grid.Owner);
            flare = SpawnFlare(map.GridCoords);
            net = SEntMan.GetNetEntity(flare.Owner);
            fuel = SEntMan.SpawnEntity("MaterialWoodPlank10", map.GridCoords);
            initialFuel = SEntMan.GetComponent<StackComponent>(fuel).Count;
            var system = Server.System<ExpendableLightSystem>();
            Assert.That(system.TryActivate(flare), Is.True);
            system.ExtinguishFlare(flare);
            AssertSpent(flare);
            deadDirty = flare.Comp.LastModifiedTick;
        });
        await Pair.RunUntilSynced();
        await AssertClientState(net, ExpendableLightState.Dead, string.Empty);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Refuel(flare, fuel), Is.True);
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.BrandNew));
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(15f));
            Assert.That(flare.Comp.LastModifiedTick, Is.GreaterThan(deadDirty));
            Assert.That(SEntMan.GetComponent<StackComponent>(fuel).Count, Is.EqualTo(initialFuel - 1));
            Assert.That(SEntMan.GetComponent<IgnitionSourceComponent>(flare.Owner).Ignited, Is.False);
        });
        await Pair.RunUntilSynced();
        await AssertClientState(net, ExpendableLightState.BrandNew, string.Empty);

        await Server.WaitAssertion(() => Assert.That(Server.System<ExpendableLightSystem>().TryActivate(flare), Is.True));
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var beforeTime = flare.Comp.StateExpiryTime;
            var beforeDirty = flare.Comp.LastModifiedTick;
            Assert.That(Refuel(flare, fuel), Is.True);
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(beforeTime + 15f));
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
            Assert.That(flare.Comp.LastModifiedTick, Is.EqualTo(beforeDirty),
                "refueling an unchanged active state only alters its server countdown");
            Assert.That(SEntMan.GetComponent<StackComponent>(fuel).Count, Is.EqualTo(initialFuel - 2));

            flare.Comp.StateExpiryTime = 285f;
            Assert.That(Refuel(flare, fuel), Is.False,
                "the existing fuel rule rejects a refill that would reach the maximum exactly");
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(285f));
            Assert.That(SEntMan.GetComponent<StackComponent>(fuel).Count, Is.EqualTo(initialFuel - 2));
            Server.System<ExpendableLightSystem>().Update(1f);
            Assert.That(Refuel(flare, fuel), Is.True);
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(299f));
            Assert.That(SEntMan.GetComponent<StackComponent>(fuel).Count, Is.EqualTo(initialFuel - 3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CorrosionShortensUnlitOrActiveBurnAndTheFadeInterval(bool alreadyActive)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var flare = SpawnFlare(map.GridCoords);
            var system = Server.System<ExpendableLightSystem>();
            if (alreadyActive)
                Assert.That(system.TryActivate(flare), Is.True);

            var corrosion = new CorrodingEvent(flare.Owner, 0, 2, XenoAcidStrength.Normal);
            SEntMan.EventBus.RaiseLocalEvent(flare.Owner, ref corrosion);
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(40f));
            Assert.That(flare.Comp.GlowDuration, Is.EqualTo(TimeSpan.FromSeconds(40)));
            Assert.That(flare.Comp.FadeOutDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
            if (!alreadyActive)
                Assert.That(system.TryActivate(flare), Is.True);

            system.Update(40f);
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Fading));
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(20f));
            corrosion = new CorrodingEvent(flare.Owner, 0, 1, XenoAcidStrength.Normal);
            SEntMan.EventBus.RaiseLocalEvent(flare.Owner, ref corrosion);
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(10f));
            system.Update(10f);
            AssertSpent(flare);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DoomStillBurnsOutAnUncontainedFlareAcrossItsTwoTransitions(bool alreadyActive)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var flare = SpawnFlare(map.GridCoords);
            var system = Server.System<ExpendableLightSystem>();
            if (alreadyActive)
                Assert.That(system.TryActivate(flare), Is.True);

            SEntMan.EnsureComponent<LightDoomedComponent>(flare.Owner);
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
            Assert.That(flare.Comp.StateExpiryTime, Is.Zero);
            system.Update(0f);
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Fading));
            Assert.That(flare.Comp.StateExpiryTime, Is.Zero);
            system.Update(0f);
            AssertSpent(flare);
        });
        // Surface synchronization failures in this test rather than the generic teardown.
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task PausedFlareKeepsItsCountdownAndResumesWithoutReplicationChurn()
    {
        var map = await Pair.CreateTestMap();
        Entity<ExpendableLightComponent> flare = default;
        float pausedTime = 0;
        GameTick initialDirty = default;
        await Server.WaitAssertion(() =>
        {
            flare = SpawnFlare(map.GridCoords);
            Assert.That(Server.System<ExpendableLightSystem>().TryActivate(flare), Is.True);
            Server.System<MetaDataSystem>().SetEntityPaused(flare.Owner, true);
            pausedTime = flare.Comp.StateExpiryTime;
            initialDirty = flare.Comp.LastModifiedTick;
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(flare.Comp.StateExpiryTime, Is.EqualTo(pausedTime));
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
            Server.System<MetaDataSystem>().SetEntityPaused(flare.Owner, false);
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(flare.Comp.StateExpiryTime, Is.LessThan(pausedTime));
            Assert.That(flare.Comp.StateExpiryTime, Is.GreaterThan(pausedTime - 1));
            Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
            Assert.That(flare.Comp.LastModifiedTick, Is.EqualTo(initialDirty));
        });
    }

    private Entity<ExpendableLightComponent> SpawnFlare(EntityCoordinates coordinates)
    {
        var uid = SEntMan.SpawnEntity("CMUExpendableLightReplicationProbe", coordinates);
        return (uid, SEntMan.GetComponent<ExpendableLightComponent>(uid));
    }

    private bool Refuel(Entity<ExpendableLightComponent> flare, EntityUid fuel)
    {
        var ev = new InteractUsingEvent(flare.Owner, fuel, flare.Owner,
            SEntMan.GetComponent<TransformComponent>(flare.Owner).Coordinates);
        SEntMan.EventBus.RaiseLocalEvent(flare.Owner, ev);
        return ev.Handled;
    }

    private void AssertSpent(Entity<ExpendableLightComponent> flare)
    {
        Assert.That(flare.Comp.CurrentState, Is.EqualTo(ExpendableLightState.Dead));
        Assert.That(flare.Comp.Activated, Is.False);
        Assert.That(SEntMan.GetComponent<IgnitionSourceComponent>(flare.Owner).Ignited, Is.False);
        Assert.That(SEntMan.GetComponent<ItemComponent>(flare.Owner).HeldPrefix, Is.EqualTo("unlit"));
        Assert.That(Server.System<TagSystem>().HasTag(flare.Owner, "Trash"), Is.True);
        Assert.That(SEntMan.GetComponent<PhysicsComponent>(flare.Owner).BodyType, Is.EqualTo(BodyType.Dynamic));
    }

    private async Task AssertClientState(NetEntity net, ExpendableLightState expected, string behavior)
    {
        await Client.WaitAssertion(() =>
        {
            var uid = CEntMan.GetEntity(net);
            Assert.That(CEntMan.GetComponent<ExpendableLightComponent>(uid).CurrentState, Is.EqualTo(expected));
            var appearance = Client.System<SharedAppearanceSystem>();
            Assert.That(appearance.TryGetData<ExpendableLightState>(uid, ExpendableLightVisuals.State, out var state), Is.True);
            Assert.That(state, Is.EqualTo(expected));
            Assert.That(appearance.TryGetData<string>(uid, ExpendableLightVisuals.Behavior, out var actualBehavior), Is.True);
            Assert.That(actualBehavior, Is.EqualTo(behavior));
        });
    }
}
