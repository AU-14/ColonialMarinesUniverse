using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server._RMC14.Atmos;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Water;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using ServerFlammableSystem = Content.Server.Atmos.EntitySystems.FlammableSystem;

namespace Content.IntegrationTests.Tests.Atmos;

[TestFixture]
[TestOf(typeof(ServerFlammableSystem))]
[TestOf(typeof(RMCFlammableSystem))]
public sealed class FlammableLifecycleTest : GameTest
{
    [TestCase("CMMobHuman")]
    [TestCase("CMXenoDrone")]
    public async Task RmcFireKeepsBurningWithoutSimulatedAtmosphere(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.SpawnEntity(prototype, map.GridCoords);
            IgniteRmc(target, 10, 10, 10);

            Server.System<ServerFlammableSystem>().Update(0f);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<FlammableComponent>(target).OnFire, Is.True,
                    "RMC fire must not go out on the first update when the map has no simulated atmosphere");
                Assert.That(TotalDamage(target), Is.GreaterThan(0),
                    "RMC incendiary fire must deal damage without simulated oxygen");
            });
        });
    }

    [Test]
    public async Task IdleUpdatesReuseSnapshotStorage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < 512; i++)
            {
                var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.EnsureComponent<FlammableComponent>(uid).NextUpdate = TimeSpan.MaxValue;
            }

            var system = Server.System<ServerFlammableSystem>();
            // Warm the snapshot capacity and update path before measuring steady-state allocations.
            system.Update(0f);
            system.Update(0f);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10; i++)
                system.Update(0f);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.LessThan(4096),
                "idle updates should not allocate a fresh snapshot of every flammable entity each tick");
        });
    }

    [Test]
    public async Task ProtectionHandlerCanAddFlammablesDuringUpdate()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<FlammableLifecycleProbeSystem>();
            SetOxygenAtmosphere(map.MapUid);
            var first = SpawnBurnable(map.MapId, 0);
            var second = SpawnBurnable(map.MapId, 1);
            var added = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<FireProtectionProbeComponent>(first).OnProtection = () =>
            {
                var flammable = SEntMan.EnsureComponent<FlammableComponent>(added);
                flammable.NextUpdate = SGameTiming.CurTime;
                flammable.FireStacks = -2f;
            };
            IgniteOrdinary(first, 4);
            IgniteOrdinary(second, 4);

            var system = Server.System<ServerFlammableSystem>();
            system.Update(0f);

            Assert.Multiple(() =>
            {
                Assert.That(TotalDamage(first), Is.GreaterThan(0));
                Assert.That(TotalDamage(second), Is.GreaterThan(0));
                Assert.That(SEntMan.GetComponent<FlammableComponent>(added).FireStacks, Is.EqualTo(-2f),
                    "components added by fire handlers should join the next update's snapshot");
            });

            system.Update(0f);
            Assert.That(SEntMan.GetComponent<FlammableComponent>(added).FireStacks, Is.EqualTo(-1f),
                "the next update should process the newly added flammable component");
        });
    }

    [Test]
    public async Task OrdinaryFireScalesWithStacksAndProtection()
    {
        var map = await Pair.CreateTestMap();
        EntityUid fourStacks = default;
        EntityUid protectedFourStacks = default;
        EntityUid twoStacks = default;

        await Server.WaitAssertion(() =>
        {
            SetOxygenAtmosphere(map.MapUid);
            fourStacks = SpawnBurnable(map.MapId, 0);
            protectedFourStacks = SpawnBurnable(map.MapId, 1);
            twoStacks = SpawnBurnable(map.MapId, 2);
            SEntMan.EnsureComponent<FireProtectionProbeComponent>(protectedFourStacks).Reduction = 0.5f;

            IgniteOrdinary(fourStacks, 4);
            IgniteOrdinary(protectedFourStacks, 4);
            IgniteOrdinary(twoStacks, 2);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var full = TotalDamage(fourStacks);
            var protectedDamage = TotalDamage(protectedFourStacks);
            var halfStacks = TotalDamage(twoStacks);
            Assert.Multiple(() =>
            {
                Assert.That(full, Is.GreaterThan(0));
                Assert.That(protectedDamage, Is.EqualTo(halfStacks).Within(0.02f),
                    "four stacks at 50% protection should equal two unprotected stacks");
                Assert.That(full, Is.EqualTo(protectedDamage * 2).Within(0.02f));
            });
        });
    }

    [Test]
    public async Task RmcMetadataUsesSpeciesFormulasAndDoesNotDuplicateIgnition()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        EntityUid protectedHuman = default;
        EntityUid intenseHuman = default;
        EntityUid steppingHuman = default;
        EntityUid xeno = default;
        EntityUid protectedHalfStackXeno = default;
        EntityUid ordinaryHuman = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<FlammableLifecycleProbeSystem>();
            SetOxygenAtmosphere(map.MapUid);
            human = SpawnBurnable(map.MapId, 0);
            protectedHuman = SpawnBurnable(map.MapId, 1);
            intenseHuman = SpawnBurnable(map.MapId, 2);
            steppingHuman = SpawnBurnable(map.MapId, 3);
            xeno = SpawnBurnable(map.MapId, 4, "CMXenoDrone");
            protectedHalfStackXeno = SpawnBurnable(map.MapId, 5, "CMXenoDrone");
            ordinaryHuman = SpawnBurnable(map.MapId, 6);

            var probe = SEntMan.EnsureComponent<FlammableLifecycleProbeComponent>(human);
            SEntMan.EnsureComponent<FireProtectionProbeComponent>(protectedHuman).Reduction = 0.5f;
            SEntMan.EnsureComponent<SteppingOnFireComponent>(steppingHuman);
            SEntMan.EnsureComponent<FireProtectionProbeComponent>(protectedHalfStackXeno).Reduction = 0.5f;

            IgniteRmc(human, 10, 10, 10);
            IgniteRmc(human, 10, 10, 10);
            IgniteRmc(protectedHuman, 10, 10, 10);
            IgniteRmc(intenseHuman, 20, 10, 10);
            IgniteRmc(steppingHuman, 10, 10, 10);
            IgniteRmc(xeno, 10, 10, 10);
            IgniteRmc(protectedHalfStackXeno, 10, 10, 5);
            IgniteOrdinary(ordinaryHuman, 2);

            var metadata = SEntMan.GetComponent<OnFireComponent>(human);
            Assert.Multiple(() =>
            {
                Assert.That(metadata.Intensity, Is.EqualTo(10));
                Assert.That(metadata.Duration, Is.EqualTo(10));
                Assert.That(probe.IgnitedEvents, Is.EqualTo(1),
                    "refreshing an already-burning entity must not raise a second ignition transition");
                Assert.That(HasAutoNetworkedField(nameof(OnFireComponent.Intensity)), Is.False);
                Assert.That(HasAutoNetworkedField(nameof(OnFireComponent.Duration)), Is.False);
                Assert.That(typeof(FlammableComponent).GetField("Intensity"), Is.Null);
                Assert.That(typeof(FlammableComponent).GetField("Duration"), Is.Null);
            });
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var humanDamage = TotalDamage(human);
            var protectedHumanDamage = TotalDamage(protectedHuman);
            var intenseHumanDamage = TotalDamage(intenseHuman);
            var steppingDamage = TotalDamage(steppingHuman);
            var xenoDamage = TotalDamage(xeno);
            var protectedHalfStackXenoDamage = TotalDamage(protectedHalfStackXeno);
            var ordinaryHumanDamage = TotalDamage(ordinaryHuman);

            Assert.Multiple(() =>
            {
                Assert.That(humanDamage, Is.GreaterThan(0));
                Assert.That(humanDamage, Is.EqualTo(ordinaryHumanDamage).Within(0.02f),
                    "RMC intensity 10 must equal two ordinary stacks because non-xeno damage is intensity/5");
                Assert.That(protectedHumanDamage, Is.EqualTo(humanDamage).Within(0.02f),
                    "the RMC non-xeno intensity/5 formula intentionally ignores fire-protection scaling");
                Assert.That(intenseHumanDamage, Is.EqualTo(humanDamage * 2).Within(0.02f));
                Assert.That(steppingDamage, Is.EqualTo(humanDamage * 2).Within(0.02f));
                Assert.That(xenoDamage, Is.GreaterThan(0));
                Assert.That(protectedHalfStackXenoDamage, Is.EqualTo(xenoDamage * 0.45f).Within(0.03f),
                    "xeno damage should normalize stacks by duration and then apply protection");
            });
        });
    }

    [Test]
    public async Task ImmunityBypassAndOxygenlessExtinguishUseCanonicalLifecycle()
    {
        var map = await Pair.CreateTestMap();
        EntityUid immune = default;
        EntityUid bypass = default;
        EntityUid oxygenless = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<FlammableLifecycleProbeSystem>();
            SetOxygenAtmosphere(map.MapUid);
            immune = SpawnBurnable(map.MapId, 0);
            bypass = SpawnBurnable(map.MapId, 1);
            oxygenless = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            PrepareBurnable(oxygenless);

            SEntMan.EnsureComponent<RMCImmuneToFireTileDamageComponent>(immune);
            SEntMan.EnsureComponent<RMCImmuneToFireTileDamageComponent>(bypass);
            SEntMan.EnsureComponent<RMCFireBypassActiveComponent>(bypass);
            SEntMan.EnsureComponent<FlammableLifecycleProbeComponent>(oxygenless);

            IgniteRmc(immune, 10, 10, 10);
            IgniteRmc(bypass, 10, 10, 10);
            IgniteOrdinary(oxygenless, 10);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(TotalDamage(immune), Is.Zero);
                Assert.That(TotalDamage(bypass), Is.GreaterThan(0));
                Assert.That(SEntMan.GetComponent<FlammableComponent>(oxygenless).OnFire, Is.False);
                Assert.That(SEntMan.GetComponent<FlammableLifecycleProbeComponent>(oxygenless).ExtinguishedEvents,
                    Is.EqualTo(1));
            });

            Server.System<RMCFlammableSystem>().Extinguish(bypass);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<OnFireComponent>(oxygenless), Is.False);
                Assert.That(SEntMan.HasComponent<OnFireComponent>(bypass), Is.False);
                Assert.That(SEntMan.HasComponent<RMCFireBypassActiveComponent>(bypass), Is.False);
                Assert.That(SEntMan.GetComponent<FlammableComponent>(bypass).OnFire, Is.False);
            });
        });
    }

    [Test]
    public async Task ResistUsesConfiguredTimeThenWaterExtinguishesBeforeAcidCleanup()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        var sawExtinguishBeforeAcidRemoval = false;

        void OnComponentRemoved(RemovedComponentEventArgs args)
        {
            if (args.BaseArgs.Owner != human || args.BaseArgs.Component is not UserAcidedComponent)
                return;

            sawExtinguishBeforeAcidRemoval =
                SEntMan.GetComponent<FlammableLifecycleProbeComponent>(human).ExtinguishedEvents > 0;
        }

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<FlammableLifecycleProbeSystem>();
            SetOxygenAtmosphere(map.MapUid);
            SEntMan.SpawnEntity("CMFloorShallowWaterEntity", map.GridCoords);
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            PrepareBurnable(human);
            SEntMan.EnsureComponent<FlammableLifecycleProbeComponent>(human);
            SEntMan.EnsureComponent<UserAcidedComponent>(human);
            SEntMan.ComponentRemoved += OnComponentRemoved;
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<RMCWaterSystem>().IsInWater(human), Is.True,
                "the fixture must be touching active uncovered water for this ordering test");

            var flammable = SEntMan.GetComponent<FlammableComponent>(human);
            flammable.ResistTime = TimeSpan.FromSeconds(0.1);
            IgniteRmc(human, 10, 10, 10);
            var expectedComplete = SGameTiming.CurTime + flammable.ResistTime;
            var alert = new ResistFireAlertEvent();
            SEntMan.EventBus.RaiseLocalEvent(human, alert);

            Assert.Multiple(() =>
            {
                Assert.That(alert.Handled, Is.True);
                Assert.That(flammable.ResistCompleteTime, Is.EqualTo(expectedComplete));
                Assert.That(flammable.OnFire, Is.False,
                    "active water should extinguish after the resist timer is started");
                Assert.That(SEntMan.GetComponent<FlammableLifecycleProbeComponent>(human).ExtinguishedEvents,
                    Is.EqualTo(1));
            });

            // The flammable scheduler normally runs once per second. Align this test update with the
            // deliberately short resist duration so the timestamp contract can be observed directly.
            flammable.NextUpdate = expectedComplete;
        });

        await Pair.RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var flammable = SEntMan.GetComponent<FlammableComponent>(human);
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(flammable.ResistCompleteTime, Is.Null);
                    Assert.That(SEntMan.HasComponent<UserAcidedComponent>(human), Is.False);
                    Assert.That(sawExtinguishBeforeAcidRemoval, Is.True,
                        "water extinguishing must occur before xeno-spit resist cleanup");
                });
            }
            finally
            {
                SEntMan.ComponentRemoved -= OnComponentRemoved;
            }
        });
    }

    [Test]
    public async Task AcidBurnKeepsStopDropRollAlertUntilResisted()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitAssertion(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            PrepareBurnable(human);
            SEntMan.EnsureComponent<UserAcidedComponent>(human);

            var alerts = Server.System<AlertsSystem>();
            Assert.That(alerts.IsShowingAlert(human, "Fire"), Is.True,
                "acid burns must show the stop-drop-roll alert");
            Assert.That(SEntMan.GetComponent<FlammableComponent>(human).OnFire, Is.False,
                "this regression covers acid burns without ordinary fire");

            Server.System<ServerFlammableSystem>().Update(0f);

            Assert.That(alerts.IsShowingAlert(human, "Fire"), Is.True,
                "the fire update must keep the alert available while acid is still burning");
            Assert.That(alerts.ActivateAlert(human, SProtoMan.Index(SEntMan.GetComponent<FlammableComponent>(human).FireAlert)), Is.True);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<UserAcidedComponent>(human), Is.False,
                    "stop-drop-roll must remove the acid burn");
                Assert.That(Server.System<AlertsSystem>().IsShowingAlert(human, "Fire"), Is.False);
            });
        });
    }

    [Test]
    public async Task StopDropRollUsesRmcResistStacksInsteadOfPositiveFade()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitAssertion(() =>
        {
            SetOxygenAtmosphere(map.MapUid);
            human = SpawnBurnable(map.MapId, 0);

            var flammable = SEntMan.GetComponent<FlammableComponent>(human);
            flammable.FirestackFade = 1;
            IgniteRmc(human, 10, 10, 10);

            var alert = new ResistFireAlertEvent();
            SEntMan.EventBus.RaiseLocalEvent(human, alert);

            Assert.Multiple(() =>
            {
                Assert.That(alert.Handled, Is.True);
                Assert.That(flammable.Resisting, Is.True);
            });
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var flammable = SEntMan.GetComponent<FlammableComponent>(human);
            Assert.Multiple(() =>
            {
                Assert.That(flammable.FireStacks, Is.Zero);
                Assert.That(flammable.OnFire, Is.False);
            });
        });
    }

    private EntityUid SpawnBurnable(MapId mapId, float x, string prototype = "CMMobHuman")
    {
        var uid = SEntMan.SpawnEntity(prototype, new MapCoordinates(new Vector2(x, 0), mapId));
        PrepareBurnable(uid);
        return uid;
    }

    private void PrepareBurnable(EntityUid uid)
    {
        var flammable = SEntMan.GetComponent<FlammableComponent>(uid);
        flammable.FirestackFade = 0;
        flammable.NextUpdate = SGameTiming.CurTime;
    }

    private void IgniteOrdinary(EntityUid uid, float stacks)
    {
        var system = Server.System<ServerFlammableSystem>();
        var flammable = SEntMan.GetComponent<FlammableComponent>(uid);
        system.SetFireStacks(uid, stacks, flammable);
        system.Ignite(uid, uid, flammable);
        flammable.NextUpdate = SGameTiming.CurTime;
    }

    private void IgniteRmc(EntityUid uid, int intensity, int duration, int maxStacks)
    {
        var flammable = SEntMan.GetComponent<FlammableComponent>(uid);
        Assert.That(Server.System<RMCFlammableSystem>().Ignite((uid, flammable), intensity, duration, maxStacks), Is.True);
        flammable.NextUpdate = SGameTiming.CurTime;
    }

    private void SetOxygenAtmosphere(EntityUid mapUid)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        Server.System<AtmosphereSystem>().SetMapAtmosphere(
            mapUid,
            false,
            new GasMixture(moles, Atmospherics.T20C));
    }

    private float TotalDamage(EntityUid uid)
    {
        return Server.System<DamageableSystem>().GetTotalDamage(uid).Float();
    }

    private static bool HasAutoNetworkedField(string field)
    {
        return typeof(OnFireComponent)
            .GetField(field, BindingFlags.Instance | BindingFlags.Public)!
            .GetCustomAttributesData()
            .Any(attribute => attribute.AttributeType.Name == "AutoNetworkedFieldAttribute");
    }
}

[RegisterComponent]
public sealed partial class FireProtectionProbeComponent : Component
{
    public float Reduction;
    public Action OnProtection;
}

[RegisterComponent]
public sealed partial class FlammableLifecycleProbeComponent : Component
{
    public int IgnitedEvents;
    public int ExtinguishedEvents;
}

public sealed class FlammableLifecycleProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FireProtectionProbeComponent, GetFireProtectionEvent>(OnGetProtection);
        SubscribeLocalEvent<FlammableLifecycleProbeComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<FlammableLifecycleProbeComponent, RMCExtinguishedEvent>(OnExtinguished);
    }

    private static void OnGetProtection(
        Entity<FireProtectionProbeComponent> ent,
        ref GetFireProtectionEvent args)
    {
        ent.Comp.OnProtection?.Invoke();
        args.Reduce(ent.Comp.Reduction);
    }

    private static void OnIgnited(Entity<FlammableLifecycleProbeComponent> ent, ref IgnitedEvent args)
    {
        ent.Comp.IgnitedEvents++;
    }

    private static void OnExtinguished(Entity<FlammableLifecycleProbeComponent> ent, ref RMCExtinguishedEvent args)
    {
        ent.Comp.ExtinguishedEvents++;
    }

}
