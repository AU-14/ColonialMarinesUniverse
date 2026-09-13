using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Nutrition;

public sealed class ToolRefinableFoodTest : InteractionTest
{
    [TestCase("FoodPizzaMargherita", "FoodPizzaMargheritaSlice", 8, 132f, 16.5f, 16.5f)]
    [TestCase("RMCFoodPizzaMargheritaFull", "RMCFoodPizzaMargheritaSlice", 6, 35f, 5.83f, 5.85f)]
    public async Task SlicingRequiresKnifeAndConservesSolution(
        string sourcePrototype,
        string resultPrototype,
        int expectedCount,
        float expectedTotal,
        float minimumPerSlice,
        float maximumPerSlice)
    {
        var target = await SpawnTarget(sourcePrototype);
        var source = ToServer(target);
        var solutionSystem = SEntMan.System<SharedSolutionContainerSystem>();

        Assert.That(SEntMan.HasComponent<ToolRefinableComponent>(source), Is.True);
        Assert.That(SEntMan.HasComponent<ToolRefinableSolutionComponent>(source), Is.True);
        Assert.That(solutionSystem.TryGetSolution(source, "food", out _, out var sourceSolution), Is.True);
        Assert.That(sourceSolution.Volume, Is.EqualTo(FixedPoint2.New(expectedTotal)));

        // The Mosin has the Slicing tool quality, but is not a knife utensil.
        await InteractUsing("WeaponSniperMosin", awaitDoAfters: false);
        await RunTicks(5);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SEntMan.Deleted(source), Is.False);
            Assert.That(ActiveDoAfters, Is.Empty, "Non-knife Slicing tools must not start refinement.");
        }

        await InteractUsing("KitchenKnife");
        await RunTicks(5);
        Assert.That(SEntMan.Deleted(source), Is.True);

        var slices = new List<EntityUid>();
        foreach (var metadata in SEntMan.EntityQuery<MetaDataComponent>())
        {
            if (!metadata.Deleted && metadata.EntityPrototype?.ID == resultPrototype)
                slices.Add(metadata.Owner);
        }

        Assert.That(slices, Has.Count.EqualTo(expectedCount));

        var total = FixedPoint2.Zero;
        foreach (var slice in slices)
        {
            Assert.That(solutionSystem.TryGetSolution(slice, "food", out _, out var sliceSolution), Is.True);
            Assert.That(sliceSolution.Volume, Is.InRange(
                FixedPoint2.New(minimumPerSlice),
                FixedPoint2.New(maximumPerSlice)));
            total += sliceSolution.Volume;
        }

        Assert.That(total, Is.EqualTo(FixedPoint2.New(expectedTotal)),
            "Tool refinement must not discard any of the source solution.");
    }
}
