using Content.IntegrationTests.Fixtures;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Botany;

[TestFixture]
public sealed class CMUPlantUpdateTest : GameTest
{
    [Test]
    public async Task WaitingForGrowthDoesNotRepublishTheHolderEveryTick()
    {
        var map = await Pair.CreateTestMap();
        EntityUid plant = default;
        PlantHolderComponent holder = default!;
        GameTick changed = default;
        TimeSpan cycle = default;
        float delay = default;
        await Server.WaitAssertion(() =>
        {
            var tray = SEntMan.SpawnEntity("hydroponicsTray", map.GridCoords);
            plant = SEntMan.SpawnEntity("CarrotPlants", map.GridCoords);
            Server.System<PlantTraySystem>().PlantingPlantInTray(tray, plant);
            holder = SEntMan.GetComponent<PlantHolderComponent>(plant);
            Server.System<PlantSystem>().ForceUpdate(plant);
            cycle = holder.LastCycle;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => changed = holder.LastModifiedTick);
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(holder.LastCycle, Is.EqualTo(cycle));
            Assert.That(holder.LastModifiedTick, Is.EqualTo(changed),
                "Waiting for the next growth cycle must not generate new holder state every tick.");
            Server.System<PlantSystem>().ForceUpdate(plant);
            Assert.That(holder.LastCycle, Is.GreaterThan(cycle), "A forced growth cycle must still run immediately.");
            cycle = holder.LastCycle;
            delay = (float) holder.CycleDelay.TotalSeconds;
        });
        await Pair.RunSeconds(delay + 0.1f);
        await Server.WaitAssertion(() =>
            Assert.That(holder.LastCycle, Is.GreaterThan(cycle), "Scheduled growth must resume after a forced cycle."));
    }

    [Test]
    public async Task RemovingPlantDataStopsGrowthUntilItIsRestored()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tray = SEntMan.SpawnEntity("hydroponicsTray", map.GridCoords);
            var plant = SEntMan.SpawnEntity("LingzhiPlants", map.GridCoords);
            Server.System<PlantTraySystem>().PlantingPlantInTray(tray, plant);
            var holder = SEntMan.GetComponent<PlantHolderComponent>(plant);
            var system = Server.System<PlantSystem>();
            SEntMan.RemoveComponent<PlantComponent>(plant);
            var cycle = holder.LastCycle;
            system.UpdatePlant((plant, holder), force: true);
            Assert.That(holder.LastCycle, Is.EqualTo(cycle), "A leftover holder cannot grow without its plant data.");
            var replacement = SEntMan.SpawnEntity("LingzhiPlants", map.GridCoords);
            SEntMan.CopyComponent(replacement, plant, SEntMan.GetComponent<PlantComponent>(replacement));
            system.UpdatePlant((plant, holder), force: true);
            Assert.That(holder.LastCycle, Is.GreaterThan(cycle));
        });
    }
}
