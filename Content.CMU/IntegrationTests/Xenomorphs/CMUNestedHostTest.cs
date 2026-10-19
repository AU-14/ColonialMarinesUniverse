using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUNestedHostTest
{
    [TestPrototypes]
    private const string Prototypes = """
    - type: entity
      id: CMUTestNestHealingHost
      components:
      - type: CMUHumanMedical
      - type: Damageable
      - type: Injurable
        damageContainer: Biological
      - type: MobState
      - type: MobThresholds
        thresholds:
          0: Alive
          80: Critical
          100: Dead
    """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task NestShortensAttachmentAndStartsIncubation(bool nestedBeforeAttachment)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid host = default;
        EntityUid parasite = default;
        TimeSpan nestedAt = default;

        await server.WaitAssertion(() =>
        {
            entities.EnsureComponent<CMUBreathingRequiredMapComponent>(map.MapUid);
            host = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            parasite = entities.SpawnEntity("CMXenoParasite", map.GridCoords);
            if (nestedBeforeAttachment)
                Nest(entities, host);
        });
        await pair.RunSeconds(1.1f);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<XenoParasiteComponent>(parasite);
            Assert.That(entities.System<SharedXenoParasiteSystem>().Infect((parasite, comp), host, force: true), Is.True);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            var delay = comp.FallOffAt!.Value - now;
            if (nestedBeforeAttachment)
                Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(15)));
            else
                Assert.That(delay, Is.InRange(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)));

            Assert.That(entities.HasComponent<VictimInfectedComponent>(host), Is.False);
            AssertCleanBreath(entities, host);
        });

        if (!nestedBeforeAttachment)
        {
            await pair.RunSeconds(2);
            await server.WaitAssertion(() => Nest(entities, host));
        }

        await server.WaitPost(() => nestedAt = server.ResolveDependency<IGameTiming>().CurTime);
        await pair.RunSeconds(1);
        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<XenoParasiteComponent>(parasite);
            Assert.That(comp.FallOffAt!.Value - nestedAt, Is.InRange(TimeSpan.FromSeconds(14), TimeSpan.FromSeconds(16)));
            Assert.That(comp.FellOff, Is.False);
            Assert.That(entities.HasComponent<VictimInfectedComponent>(host), Is.False);
            AssertCleanBreath(entities, host);
        });

        await pair.RunSeconds(15);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<XenoParasiteComponent>(parasite).FellOff, Is.True);
            Assert.That(entities.GetComponent<InfectableComponent>(host).BeingInfected, Is.False);
            Assert.That(entities.GetComponent<VictimInfectedComponent>(host).IncubationMultiplier, Is.EqualTo(1.5f));
            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(host, "mask", out _), Is.False);
            var breath = new InhaleLocationEvent(null, entities.GetComponent<RespiratorComponent>(host));
            entities.EventBus.RaiseLocalEvent(host, ref breath);
            Assert.That(breath.Gas, Is.Null, "A fallen-off parasite must no longer supply air.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnnestedHostKeepsNormalAttachmentAndBreathesCleanAir()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid host = default;
        EntityUid parasite = default;

        await server.WaitAssertion(() =>
        {
            entities.EnsureComponent<CMUBreathingRequiredMapComponent>(map.MapUid);
            host = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            parasite = entities.SpawnEntity("CMXenoParasite", map.GridCoords);
        });
        await pair.RunSeconds(1.1f);
        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<XenoParasiteComponent>(parasite);
            Assert.That(entities.System<SharedXenoParasiteSystem>().Infect((parasite, comp), host, force: true), Is.True);
        });
        await pair.RunSeconds(16);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<XenoParasiteComponent>(parasite).FellOff, Is.False);
            Assert.That(entities.HasComponent<VictimInfectedComponent>(host), Is.False);
            AssertCleanBreath(entities, host);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(60, true, false, false, 57)]
    [TestCase(50.25, true, false, false, 50)]
    [TestCase(50, true, false, false, 50)]
    [TestCase(49, true, false, false, 49)]
    [TestCase(90, true, false, false, 87)]
    [TestCase(100, true, false, false, 100)]
    [TestCase(60, false, false, false, 60)]
    [TestCase(60, true, true, false, 60)]
    [TestCase(60, true, false, true, 60)]
    public async Task NestHealingIsCappedAndOnlyAffectsLivingNestedHumans(
        double initialDamage, bool nested, bool detached, bool nonHuman, double expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid host = default;

        await server.WaitAssertion(() =>
        {
            host = entities.SpawnEntity("CMUTestNestHealingHost", map.GridCoords);
            if (nonHuman)
                entities.RemoveComponent<CMUHumanMedicalComponent>(host);
            if (nested)
            {
                Nest(entities, host);
#pragma warning disable RA0002
                entities.GetComponent<XenoNestedComponent>(host).Detached = detached;
#pragma warning restore RA0002
            }
        });

        // Establish the periodic update's phase before measuring exactly three healing ticks.
        await pair.RunSeconds(0.1f);
        await server.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier();
            var total = FixedPoint2.New(initialDamage);
            damage.DamageDict["Cold"] = total / 2;
            damage.DamageDict["Poison"] = total - damage.DamageDict["Cold"];
            entities.System<DamageableSystem>().TryChangeDamage(host, damage, ignoreResistances: true);
            Assert.That(entities.System<DamageableSystem>().GetTotalDamage(host), Is.EqualTo(FixedPoint2.New(initialDamage)));
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
            Assert.That(entities.System<DamageableSystem>().GetTotalDamage(host), Is.EqualTo(FixedPoint2.New(expected))));
        await pair.CleanReturnAsync();
    }

    private static void Nest(IEntityManager entities, EntityUid host)
    {
        var nest = entities.SpawnEntity("XenoNest", entities.GetComponent<TransformComponent>(host).Coordinates);
#pragma warning disable RA0002
        entities.GetComponent<XenoNestComponent>(nest).Nested = host;
        entities.EnsureComponent<XenoNestedComponent>(host).Nest = nest;
#pragma warning restore RA0002
        entities.System<SharedTransformSystem>().SetCoordinates(host, new EntityCoordinates(nest, Vector2.Zero));
    }

    private static void AssertCleanBreath(IEntityManager entities, EntityUid host)
    {
        var vacuumBreath = new InhaleLocationEvent(null, entities.GetComponent<RespiratorComponent>(host));
        entities.EventBus.RaiseLocalEvent(host, ref vacuumBreath);
        Assert.That(vacuumBreath.Gas, Is.Not.Null, "The parasite supplies air even in a vacuum.");
        Assert.That(entities.System<RespiratorSystem>().CanMetabolizeInhaledAir(host, vacuumBreath.Gas!), Is.True);

        var pollutedAir = new GasMixture(Atmospherics.CellVolume);
        pollutedAir.AdjustMoles(Gas.Plasma, Atmospherics.OxygenMolesStandard);
        var breath = new InhaleLocationEvent(pollutedAir, entities.GetComponent<RespiratorComponent>(host));
        entities.EventBus.RaiseLocalEvent(host, ref breath);
        Assert.That(breath.Gas, Is.Not.Null.And.Not.SameAs(pollutedAir));
        Assert.That(breath.Gas!.GetMoles(Gas.Oxygen), Is.GreaterThan(0));
        Assert.That(breath.Gas.GetMoles(Gas.Plasma), Is.Zero);
        Assert.That(entities.System<RespiratorSystem>().CanMetabolizeInhaledAir(host, breath.Gas), Is.True);
    }
}
