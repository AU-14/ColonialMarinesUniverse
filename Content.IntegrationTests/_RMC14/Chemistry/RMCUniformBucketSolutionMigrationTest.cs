using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Chemistry;

[TestFixture]
[TestOf(typeof(SolutionComponent))]
public sealed class RMCUniformBucketSolutionMigrationTest : GameTest
{
    private static readonly string[] Uniforms =
    [
        "JumpsuitMarine",
        "CMJumpsuitAverageJoe",
        "RMCJumpsuitMarinePatch",
    ];

    private static readonly IReadOnlyDictionary<string, int> Buckets =
        new Dictionary<string, int>
        {
            ["RMCBucket"] = 120,
            ["CMBucketMop"] = 240,
            ["RMCBucketJanitorial"] = 500,
            ["RMCReagentJug"] = 500,
        };

    [SidedDependency(Side.Server)]
    private SharedSolutionContainerSystem _solutions = default!;

    [Test]
    public async Task UniformBaseProvidesOneDirectFiberFoodSolutionAcrossDiamondInheritance()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            foreach (var prototypeId in Uniforms)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
                AssertPrototypeSolution(prototype, "food", 30, "Fiber", 30);
                Assert.That(prototype.TryComp<EdibleComponent>(out var edible, SEntMan.ComponentFactory), Is.True,
                    prototypeId);
                Assert.That(edible!.Solution, Is.EqualTo("food"), prototypeId);

                var uniform = SEntMan.SpawnEntity(prototypeId, map.GridCoords);
                AssertSpawnedSolution(uniform, "food", 30, "Fiber", 30, prototypeId);
                var liveEdible = SEntMan.GetComponent<EdibleComponent>(uniform);
                Assert.That(_solutions.TryGetSolution(uniform, liveEdible.Solution, out var edibleSolution, out _),
                    Is.True, prototypeId);
                Assert.That(edibleSolution!.Value.Owner, Is.EqualTo(uniform), prototypeId);
            }
        });
    }

    [Test]
    public async Task BucketFamilyUsesDirectSelfOwnedSolutionsAndPreservesJanitorialTransferContract()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            foreach (var (prototypeId, capacity) in Buckets)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
                AssertPrototypeSolution(prototype, "bucket", capacity, null, 0);

                var bucket = SEntMan.SpawnEntity(prototypeId, map.GridCoords);
                AssertSpawnedSolution(bucket, "bucket", capacity, null, 0, prototypeId);
                Assert.That(_solutions.TryGetRefillableSolution(bucket, out var refillable, out _), Is.True,
                    prototypeId);
                Assert.That(refillable!.Value.Owner, Is.EqualTo(bucket), prototypeId);
                Assert.That(_solutions.TryGetDrainableSolution(bucket, out var drainable, out _), Is.True,
                    prototypeId);
                Assert.That(drainable!.Value.Owner, Is.EqualTo(bucket), prototypeId);
            }

            var janitorial = SProtoMan.Index<EntityPrototype>("RMCBucketJanitorial");
            Assert.That(janitorial.TryComp<SolutionTransferComponent>(out var transfer, SEntMan.ComponentFactory),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(transfer!.TransferAmount, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(transfer.MinimumTransferAmount, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(transfer.MaximumTransferAmount, Is.EqualTo(FixedPoint2.New(500)));
                Assert.That(transfer.TransferAmounts,
                    Is.EqualTo(new[] { 5, 10, 15, 20, 25, 30, 40, 60, 80, 100, 120, 240, 300, 500 }
                        .Select(FixedPoint2.New)));
            });
        });
    }

    private void AssertPrototypeSolution(
        EntityPrototype prototype,
        string solutionId,
        int capacity,
        string? reagent,
        int quantity)
    {
        var factory = SEntMan.ComponentFactory;
        var enumerated = _solutions.EnumerateSolutions(prototype).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(prototype.TryComp<SolutionComponent>(out _, factory), Is.True, prototype.ID);
            Assert.That(prototype.TryComp<SolutionContainerManagerComponent>(out _, factory), Is.False, prototype.ID);
            Assert.That(prototype.TryComp<SolutionManagerComponent>(out _, factory), Is.False, prototype.ID);
            Assert.That(enumerated, Has.Length.EqualTo(1), prototype.ID);
            Assert.That(enumerated[0].Id, Is.EqualTo(solutionId), prototype.ID);
            AssertSolution(enumerated[0].Solution, capacity, reagent, quantity, prototype.ID);
        });
    }

    private void AssertSpawnedSolution(
        EntityUid owner,
        string solutionId,
        int capacity,
        string? reagent,
        int quantity,
        string prototypeId)
    {
        Assert.That(_solutions.TryGetSolution(owner, solutionId, out var solutionEntity, out var solution),
            Is.True, prototypeId);
        Assert.Multiple(() =>
        {
            Assert.That(solutionEntity!.Value.Owner, Is.EqualTo(owner), prototypeId);
            Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(owner), Is.False, prototypeId);
            Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(owner), Is.False, prototypeId);
            AssertSolution(solution!, capacity, reagent, quantity, prototypeId);
        });
    }

    private static void AssertSolution(
        Solution solution,
        int capacity,
        string? reagent,
        int quantity,
        string prototypeId)
    {
        Assert.Multiple(() =>
        {
            Assert.That(solution.MaxVolume, Is.EqualTo(FixedPoint2.New(capacity)), prototypeId);
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(quantity)), prototypeId);
            Assert.That(solution.Contents, Has.Count.EqualTo(reagent is null ? 0 : 1), prototypeId);
            if (reagent is not null)
            {
                Assert.That(solution.Contents[0].Reagent.Prototype, Is.EqualTo(reagent), prototypeId);
                Assert.That(solution.Contents[0].Quantity, Is.EqualTo(FixedPoint2.New(quantity)), prototypeId);
            }
        });
    }
}
