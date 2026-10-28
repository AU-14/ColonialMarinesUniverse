using Content.IntegrationTests.Fixtures;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests.CMU14.Botany;

public sealed class CMUSoilNutrientTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task NutrientsFeedSoilAndAreConsumedWithOrWithoutAPlant(bool planted)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tray = SEntMan.SpawnEntity("hydroponicsTray", map.GridCoords);
            var trays = Server.System<PlantTraySystem>();
            if (planted)
                trays.PlantingPlantInTray(tray, SEntMan.SpawnEntity("CarrotPlants", map.GridCoords));

            var component = SEntMan.GetComponent<PlantTrayComponent>(tray);
            trays.AdjustNutrient(tray, -100);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(tray, "soil", out var soil), Is.True);
            Assert.That(solutions.TryAddReagent(soil!.Value, "EZNutrient", 5), Is.True);

            trays.UpdateReagents(tray);

            Assert.That(component.NutritionLevel, Is.EqualTo(2), "Tray effects must run exactly once, even on empty soil.");
            Assert.That(soil.Value.Comp.Solution.GetTotalPrototypeQuantity("EZNutrient"), Is.EqualTo(FixedPoint2.New(4)));
        });
    }
}
