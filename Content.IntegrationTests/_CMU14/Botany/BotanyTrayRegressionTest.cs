using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Botany;

[TestFixture]
public sealed class BotanyTrayRegressionTest
{
    private static readonly ProtoId<ReagentPrototype> Water = "Water";

    [Test]
    public async Task PouringOntoPlantFillsParentTray()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var solutions = entities.System<SharedSolutionContainerSystem>();
            var interactions = entities.System<SharedInteractionSystem>();
            var user = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var tray = entities.SpawnEntity("hydroponicsTray", MapCoordinates.Nullspace);
            var plant = entities.SpawnEntity("CarrotPlants", MapCoordinates.Nullspace);
            var glass = entities.SpawnEntity("DrinkGlass", MapCoordinates.Nullspace);

            entities.System<PlantTraySystem>().PlantingPlantInTray(tray, plant);
            Assert.That(solutions.TryGetSolution(glass, "drink", out var glassSolution), Is.True);
            Assert.That(solutions.TryAddReagent(glassSolution!.Value, Water, 10), Is.True);
            Assert.That(solutions.TryGetSolution(tray, "soil", out _, out var soil), Is.True);

            var handled = interactions.InteractDoAfter(
                user,
                glass,
                plant,
                entities.GetComponent<TransformComponent>(plant).Coordinates,
                canReach: true);

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True, "Clicking the plant did not redirect pouring to its tray.");
                Assert.That(soil!.Volume, Is.EqualTo((FixedPoint2) 5));
                Assert.That(glassSolution.Value.Comp.Solution.Volume, Is.EqualTo((FixedPoint2) 5));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClickingTrayHarvestsContainedPlant()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var user = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var tray = entities.SpawnEntity("hydroponicsTray", MapCoordinates.Nullspace);
            var plant = entities.SpawnEntity("CarrotPlants", MapCoordinates.Nullspace);
            var holder = entities.GetComponent<PlantHolderComponent>(plant);
            holder.ReadyForHarvest = true;
            entities.System<PlantTraySystem>().PlantingPlantInTray(tray, plant);

            var interact = new InteractHandEvent(user, tray);
            entities.EventBus.RaiseLocalEvent(tray, interact);

            Assert.Multiple(() =>
            {
                Assert.That(interact.Handled, Is.True, "The tray click was not forwarded to its plant.");
                Assert.That(holder.ReadyForHarvest, Is.False, "The contained plant was not harvested.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TrayResourceConsumptionIsFortyPercentLower()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var tray = entities.SpawnEntity("hydroponicsTray", MapCoordinates.Nullspace);
            var component = entities.GetComponent<PlantTrayComponent>(tray);

            Assert.That(component.TrayConsumptionMultiplier, Is.EqualTo(1.2f));
        });

        await pair.CleanReturnAsync();
    }
}
