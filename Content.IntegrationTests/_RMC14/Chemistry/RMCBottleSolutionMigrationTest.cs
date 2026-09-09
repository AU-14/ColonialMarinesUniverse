using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Medical.Refill;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Nutrition.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Chemistry;

[TestFixture]
[TestOf(typeof(SolutionComponent))]
public sealed class RMCBottleSolutionMigrationTest : GameTest
{
    private const string SolutionName = "drink";

    private static readonly IReadOnlyDictionary<string, BottleSpec> Bottles =
        new Dictionary<string, BottleSpec>
        {
            ["CMBottleEmpty"] = new(60),
            ["CMBottleBicaridine"] = new(60, new ExpectedReagent("CMBicaridine", 60)),
            ["CMBottleDexalin"] = new(60, new ExpectedReagent("CMDexalin", 60)),
            ["CMBottleDylovene"] = new(60, new ExpectedReagent("CMDylovene", 60)),
            ["CMBottleInaprovaline"] = new(60, new ExpectedReagent("CMInaprovaline", 60)),
            ["CMBottleKelotane"] = new(60, new ExpectedReagent("CMKelotane", 60)),
            ["CMBottleTricordrazine"] = new(60, new ExpectedReagent("CMTricordrazine", 60)),
            ["RMCBottleAntiZed"] = new(60, new ExpectedReagent("RMCAntiZed", 60)),
            ["RMCBottleMindbreaker"] = new(60, new ExpectedReagent("RMCMindbreakerToxin", 60)),
            ["RMCLargeBottleEmpty"] = new(140),
            ["RMCLargeBottleDexalinPlus"] = new(140, new ExpectedReagent("CMDexalinPlus", 140)),
            ["RMCLargeBottleMeralyneBicaridine"] = new(
                140,
                new ExpectedReagent("CMMeralyne", 70),
                new ExpectedReagent("CMBicaridine", 70)),
            ["RMCLargeBottleKelotaneDermaline"] = new(
                140,
                new ExpectedReagent("CMKelotane", 70),
                new ExpectedReagent("CMDermaline", 70)),
        };

    [SidedDependency(Side.Server)]
    private SharedSolutionContainerSystem _solutions = default!;

    [Test]
    public async Task AllBottleSolutionsAreDirectAndPreserveContentsAndConsumers()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Bottles, Has.Count.EqualTo(13));
            var factory = SEntMan.ComponentFactory;

            foreach (var (prototypeId, expected) in Bottles)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
                var enumerated = _solutions.EnumerateSolutions(prototype).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryComp<SolutionComponent>(out _, factory), Is.True, prototypeId);
                    Assert.That(prototype.TryComp<SolutionContainerManagerComponent>(out _, factory), Is.False,
                        prototypeId);
                    Assert.That(prototype.TryComp<SolutionManagerComponent>(out _, factory), Is.False, prototypeId);
                    Assert.That(_solutions.TryGetSolution(prototype, SolutionName, out var prototypeSolution), Is.True,
                        prototypeId);
                    AssertSolution(prototypeSolution!, expected, prototypeId);
                    Assert.That(enumerated, Has.Length.EqualTo(1),
                        $"{prototypeId} must enumerate its direct solution exactly once");
                    Assert.That(enumerated[0].Id, Is.EqualTo(SolutionName), prototypeId);
                    AssertSolution(enumerated[0].Solution, expected, $"{prototypeId} enumerated");
                });

                AssertSolutionConsumers(prototype, expected, factory);

                var bottle = SEntMan.SpawnEntity(prototypeId, map.GridCoords);
                Assert.That(_solutions.TryGetSolution(bottle, SolutionName, out var solutionEntity, out var solution),
                    Is.True, prototypeId);
                Assert.Multiple(() =>
                {
                    Assert.That(solutionEntity!.Value.Owner, Is.EqualTo(bottle),
                        $"{prototypeId} must own its sole solution directly");
                    Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(bottle), Is.False, prototypeId);
                    Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(bottle), Is.False, prototypeId);
                    AssertSolution(solution!, expected, prototypeId);
                });
            }
        });
    }

    [Test]
    public async Task SmallAndMixedLargeBottlesReplicateFullHalfAndEmptyFillVisuals()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid small = default;
        EntityUid large = default;
        NetEntity smallNet = default;
        NetEntity largeNet = default;
        Entity<SolutionComponent> smallSolution = default;
        Entity<SolutionComponent> largeSolution = default;

        try
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, map.Grid.Owner);
                small = SEntMan.SpawnEntity("CMBottleBicaridine", map.GridCoords);
                large = SEntMan.SpawnEntity("RMCLargeBottleMeralyneBicaridine", map.GridCoords);
                smallNet = SEntMan.GetNetEntity(small);
                largeNet = SEntMan.GetNetEntity(large);
                Assert.That(_solutions.TryGetSolution(small, SolutionName, out var smallEntity, out _), Is.True);
                Assert.That(_solutions.TryGetSolution(large, SolutionName, out var largeEntity, out _), Is.True);
                smallSolution = smallEntity!.Value;
                largeSolution = largeEntity!.Value;
                AssertServerState(smallSolution, 60, 1f);
                AssertServerState(largeSolution, 140, 1f);
            });
            await Pair.RunUntilSynced();

            await AssertClientFill(smallNet, 60, "bottle-1-5", visible: true);
            await AssertClientFill(largeNet, 140, "bottle-1-5", visible: true);

            await Server.WaitPost(() =>
            {
                Assert.That(_solutions.RemoveReagent(smallSolution, "CMBicaridine", FixedPoint2.New(30)),
                    Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(_solutions.RemoveReagent(largeSolution, "CMMeralyne", FixedPoint2.New(35)),
                    Is.EqualTo(FixedPoint2.New(35)));
                Assert.That(_solutions.RemoveReagent(largeSolution, "CMBicaridine", FixedPoint2.New(35)),
                    Is.EqualTo(FixedPoint2.New(35)));
                AssertServerState(smallSolution, 30, 0.5f);
                AssertServerState(largeSolution, 70, 0.5f);
            });
            await Pair.RunUntilSynced();

            await AssertClientFill(smallNet, 30, "bottle-1-2", visible: true);
            await AssertClientFill(largeNet, 70, "bottle-1-2", visible: true);

            await Server.WaitPost(() =>
            {
                _solutions.RemoveAllSolution(smallSolution);
                _solutions.RemoveAllSolution(largeSolution);
                AssertServerState(smallSolution, 0, 0f);
                AssertServerState(largeSolution, 0, 0f);
            });
            await Pair.RunUntilSynced();

            await AssertClientFill(smallNet, 0, "bottle-1-2", visible: false);
            await AssertClientFill(largeNet, 0, "bottle-1-2", visible: false);
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                if (SEntMan.EntityExists(small))
                    SEntMan.DeleteEntity(small);
                if (SEntMan.EntityExists(large))
                    SEntMan.DeleteEntity(large);
            });
        }

        void AssertServerState(Entity<SolutionComponent> solutionEntity, int volume, float fillFraction)
        {
            var solution = SEntMan.GetComponent<SolutionComponent>(solutionEntity).Solution;
            var appearance = Server.System<SharedAppearanceSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(volume)));
                Assert.That(appearance.TryGetData<float>(
                    solutionEntity.Owner,
                    SolutionContainerVisuals.FillFraction,
                    out var actualFraction), Is.True);
                Assert.That(actualFraction, Is.EqualTo(fillFraction));
            });
        }

        async Task AssertClientFill(NetEntity bottleNet, int volume, string state, bool visible)
        {
            await Client.WaitAssertion(() =>
            {
                var bottle = CEntMan.GetEntity(bottleNet);
                var sprite = CEntMan.GetComponent<SpriteComponent>(bottle);
                var solution = CEntMan.GetComponent<SolutionComponent>(bottle).Solution;
                var sprites = Client.System<SpriteSystem>();
                Assert.That(sprites.LayerMapTryGet(
                    (bottle, sprite),
                    SolutionContainerLayers.Fill,
                    out var layer,
                    false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(volume)));
                    Assert.That(sprite[layer].RsiState.Name, Is.EqualTo(state));
                    Assert.That(sprite[layer].Visible, Is.EqualTo(visible));
                });
            });
        }
    }

    private static void AssertSolution(
        Solution solution,
        BottleSpec expected,
        string prototypeId)
    {
        var actualReagents = solution.Contents
            .Select(reagent => new ExpectedReagent(reagent.Reagent.Prototype, reagent.Quantity.Int()))
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(solution.MaxVolume, Is.EqualTo(FixedPoint2.New(expected.Capacity)), prototypeId);
            Assert.That(solution.Temperature, Is.EqualTo(293.15f), prototypeId);
            Assert.That(solution.CanReact, Is.True, prototypeId);
            Assert.That(solution.Volume,
                Is.EqualTo(FixedPoint2.New(expected.Reagents.Sum(reagent => reagent.Quantity))), prototypeId);
            Assert.That(actualReagents, Is.EqualTo(expected.Reagents),
                $"{prototypeId} reagent declaration order and quantities");
        });
    }

    private static void AssertSolutionConsumers(
        EntityPrototype prototype,
        BottleSpec expected,
        IComponentFactory factory)
    {
        AssertSolutionName<MixableSolutionComponent>(prototype, component => component.Solution, factory);
        AssertSolutionName<RefillableSolutionComponent>(prototype, component => component.Solution, factory);
        AssertSolutionName<DrainableSolutionComponent>(prototype, component => component.Solution, factory);
        AssertSolutionName<ExaminableSolutionComponent>(prototype, component => component.Solution, factory);
        AssertSolutionName<DrawableSolutionComponent>(prototype, component => component.Solution, factory);
        AssertSolutionName<EdibleComponent>(prototype, component => component.Solution, factory);
        AssertSolutionName<SpillableComponent>(prototype, component => component.SolutionName, factory);

        Assert.That(prototype.TryComp<CMRefillableSolutionComponent>(out var refillable, factory),
            Is.EqualTo(expected.Reagents.Length > 0), prototype.ID);
        if (refillable != null)
            Assert.That(refillable.Solution, Is.EqualTo(SolutionName), prototype.ID);
    }

    private static void AssertSolutionName<T>(
        EntityPrototype prototype,
        Func<T, string> getName,
        IComponentFactory factory)
        where T : Component, new()
    {
        Assert.That(prototype.TryComp<T>(out var component, factory), Is.True, $"{prototype.ID} {typeof(T).Name}");
        Assert.That(getName(component!), Is.EqualTo(SolutionName), $"{prototype.ID} {typeof(T).Name}");
    }

    private sealed record BottleSpec(int Capacity, params ExpectedReagent[] Reagents);

    private readonly record struct ExpectedReagent(string Id, int Quantity);
}
