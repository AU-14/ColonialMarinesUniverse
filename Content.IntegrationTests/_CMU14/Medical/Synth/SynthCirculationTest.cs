#pragma warning disable RA0002 // tests poke timers and tunables directly

using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Synth;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Medical.Synth;

[TestFixture]
public sealed class SynthCirculationTest
{
    [Test]
    public async Task SynthsTakeIvButStayUninjectable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var synth = SpawnSynth(entities, map.GridCoords);
            Assert.That(entities.HasComponent<IVDripTargetComponent>(synth), Is.True);
            Assert.That(entities.HasComponent<CMUSynthCirculationComponent>(synth), Is.True);
            Assert.That(entities.HasComponent<InjectableSolutionComponent>(synth), Is.False,
                "syringes and hyposprays still shouldn't load chems into a synth");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SynthBloodPackRefillsSynthAndBloodTypesDontMix()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid synth = default;
        float drained = 0;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var bloodstream = entities.System<BloodstreamSystem>();
            synth = SpawnSynth(entities, map.GridCoords);
            var human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var humanPack = entities.SpawnEntity("CMBloodPackFull", map.GridCoords);
            var synthPack = entities.SpawnEntity("CMUSynthBloodPackFull", map.GridCoords);

            Assert.That(PackBlocked(entities, human, humanPack, synth), Is.True, "human blood into a synth");
            Assert.That(PackBlocked(entities, human, synthPack, human), Is.True, "synth blood into a human");
            Assert.That(PackBlocked(entities, human, synthPack, synth), Is.False);
            Assert.That(PackBlocked(entities, synth, humanPack, human), Is.False, "normal transfusions untouched");

            Assert.That(bloodstream.TryModifyBloodLevel(synth, FixedPoint2.New(-300)), Is.True);
            drained = bloodstream.GetBloodLevel(synth);
            Assert.That(drained, Is.LessThan(0.5f));

            var pack = entities.GetComponent<BloodPackComponent>(synthPack);
            pack.AttachedTo = synth;
            pack.TransferAt = server.ResolveDependency<IGameTiming>().CurTime;
        });

        await server.WaitRunTicks(300);
        await server.WaitAssertion(() =>
        {
            var level = server.EntMan.System<BloodstreamSystem>().GetBloodLevel(synth);
            Assert.That(level, Is.GreaterThan(drained), "the synth pack should refill synth blood");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForeignReagentsGetFlushedFromSynthBlood()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid synth = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            synth = SpawnSynth(entities, map.GridCoords);
            var bloodstream = entities.System<BloodstreamSystem>();
            Assert.That(bloodstream.TryModifyBloodLevel(synth, FixedPoint2.New(-100)), Is.True);
            Assert.That(bloodstream.TryAddToBloodstream(synth, new([new("Blood", FixedPoint2.New(20))])), Is.True);
            Assert.That(BloodSolution(entities, synth).GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.New(20)));
        });

        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var solution = BloodSolution(server.EntMan, synth);
            Assert.That(solution.GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(solution.GetTotalPrototypeQuantity("RMCSynthBlood"), Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpenStumpsDontDrainSynths()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid synth = default, human = default;
        float synthBefore = 0, humanBefore = 0;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            synth = SpawnSynth(entities, map.GridCoords);
            human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            AddOpenStump(entities, synth);
            AddOpenStump(entities, human);
            var bloodstream = entities.System<BloodstreamSystem>();
            synthBefore = bloodstream.GetBloodLevel(synth);
            humanBefore = bloodstream.GetBloodLevel(human);
        });

        await server.WaitRunTicks(150);
        await server.WaitAssertion(() =>
        {
            var bloodstream = server.EntMan.System<BloodstreamSystem>();
            Assert.That(bloodstream.GetBloodLevel(human), Is.LessThan(humanBefore), "stumps still bleed humans");
            Assert.That(bloodstream.GetBloodLevel(synth), Is.EqualTo(synthBefore).Within(0.0001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WelderMendsSynthOrgansOnceThePlatingIsFixed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid synth = default, brain = default;
        FixedPoint2 damaged = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            synth = SpawnSynth(entities, map.GridCoords);
            entities.GetComponent<CMUSynthCirculationComponent>(synth).OrganRepairTime = TimeSpan.FromSeconds(0.1);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<CMUBrainComponent>(synth, out brain), Is.True);

            var health = entities.GetComponent<OrganHealthComponent>(brain);
            var hit = health.Current - health.StageThresholds[OrganDamageStage.Damaged];
            var ev = new OrganDamagedEvent(synth, brain, new DamageSpecifier { DamageDict = { ["Blunt"] = hit } }, OrganDamageSource.Direct);
            entities.EventBus.RaiseLocalEvent(brain, ref ev, broadcast: true);
            Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Damaged));
            damaged = health.Current;
        });

        // the actual bug: nothing ever heals a synth organ by itself, so the brain stays damaged
        await server.WaitRunTicks(450);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<OrganHealthComponent>(brain).Current, Is.EqualTo(damaged));

            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var welder = entities.SpawnEntity("CMWelder", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder, checkActionBlocker: false), Is.True);
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((welder, null), user), Is.True);

            var interaction = new InteractUsingEvent(user, welder, synth, entities.GetComponent<TransformComponent>(synth).Coordinates);
            entities.EventBus.RaiseLocalEvent(synth, interaction);
            Assert.That(interaction.Handled, Is.True, "a welder on a dented-free synth with a hurt brain should start internal repair");
        });

        await server.WaitRunTicks(120);
        await server.WaitAssertion(() =>
        {
            var health = server.EntMan.GetComponent<OrganHealthComponent>(brain);
            Assert.That(health.Current, Is.EqualTo(health.Max), "repair repeats until the organ is whole");
            Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SynthBloodCanBeMadeFromDispenserStock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(Mix(entities, map, ("RMCSilicon", 5), ("RMCChlorine", 5), ("RMCMethane", 5), ("Water", 5))
                .GetTotalPrototypeQuantity("CMUSiliconeOil"), Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(Mix(entities, map, ("RMCEthanol", 5), ("RMCOxygen", 5))
                .GetTotalPrototypeQuantity("CMUGlycol"), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(Mix(entities, map, ("RMCMethane", 5), ("RMCSulfur", 5), ("CMUAmmonia", 5))
                .GetTotalPrototypeQuantity("CMUSyntheticLatex"), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(Mix(entities, map, ("RMCSodium", 5), ("RMCSulphuricAcid", 5), ("RMCCarbon", 5))
                .GetTotalPrototypeQuantity("CMUSurfactant"), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(Mix(entities, map, ("RMCSodium", 5), ("RMCPhosphorus", 5), ("RMCOxygen", 5))
                .GetTotalPrototypeQuantity("CMUCorrosionInhibitor"), Is.EqualTo(FixedPoint2.New(15)));

            var blood = Mix(entities, map, ("Water", 30), ("CMUSyntheticLatex", 20), ("CMUSiliconeOil", 20),
                ("CMUGlycol", 10), ("CMUSurfactant", 10), ("CMUCorrosionInhibitor", 10));
            Assert.That(blood.GetTotalPrototypeQuantity("RMCSynthBlood"), Is.EqualTo(FixedPoint2.New(100)));
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnSynth(IEntityManager entities, Robust.Shared.Map.EntityCoordinates coords)
    {
        var synth = entities.SpawnEntity("CMMobHuman", coords);
        entities.EnsureComponent<SynthComponent>(synth);
        return synth;
    }

    private static bool PackBlocked(IEntityManager entities, EntityUid user, EntityUid pack, EntityUid target)
    {
        var ev = new BeforeRangedInteractEvent(user, pack, target, entities.GetComponent<TransformComponent>(target).Coordinates, true);
        entities.EventBus.RaiseLocalEvent(pack, ev);
        return ev.Handled;
    }

    private static Content.Shared.Chemistry.Components.Solution BloodSolution(IEntityManager entities, EntityUid body)
    {
        var name = entities.GetComponent<BloodstreamComponent>(body).BloodSolutionName;
        Assert.That(entities.System<SharedSolutionContainerSystem>().TryGetSolution(body, name, out _, out var solution), Is.True);
        return solution!;
    }

    private static void AddOpenStump(IEntityManager entities, EntityUid body)
    {
        var root = entities.System<SharedBodySystem>().GetRootPartOrNull(body);
        Assert.That(root, Is.Not.Null);
        var stumps = entities.EnsureComponent<CMUOpenStumpComponent>(root!.Value.Entity);
        stumps.Stumps.Add(new CMUStump { Type = BodyPartType.Arm, Symmetry = BodyPartSymmetry.Left });
        stumps.NextBleed = TimeSpan.Zero;
    }

    private static Content.Shared.Chemistry.Components.Solution Mix(
        IEntityManager entities,
        Robust.UnitTesting.Pool.TestMapData map,
        params (string Reagent, int Amount)[] reagents)
    {
        var solutions = entities.System<SharedSolutionContainerSystem>();
        var beaker = entities.SpawnEntity("CMBeakerLarge", map.GridCoords);
        Assert.That(solutions.TryGetSolution(beaker, "beaker", out var soln, out var solution), Is.True);
        foreach (var (reagent, amount) in reagents)
        {
            Assert.That(solutions.TryAddReagent(soln!.Value, reagent, FixedPoint2.New(amount)), Is.True, reagent);
        }

        return solution!;
    }
}
