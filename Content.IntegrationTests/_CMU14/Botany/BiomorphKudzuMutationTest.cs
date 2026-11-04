using Content.IntegrationTests.Fixtures;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;

namespace Content.IntegrationTests.CMU14.Botany;

[TestFixture]
public sealed class BiomorphKudzuMutationTest : GameTest
{
    [Test]
    public async Task OvergrownMutatedPlantBecomesBiomorphTendons()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tray = SEntMan.SpawnEntity("hydroponicsTray", map.GridCoords);
            var plant = SEntMan.SpawnEntity("CarrotPlants", map.GridCoords);
            var trays = SEntMan.System<PlantTraySystem>();
            trays.PlantingPlantInTray(tray, plant);
            var trait = SEntMan.EnsureComponent<PlantTraitKudzuComponent>(plant);
            var trayComponent = SEntMan.GetComponent<PlantTrayComponent>(tray);
            var grow = new PlantGrowEvent(SEntMan.GetNetEntity(tray));

            trays.AdjustWater(tray, -trayComponent.WaterLevel);
            trays.AdjustWeed(tray, trait.WeedLevelThreshold - 1 - trayComponent.WeedLevel);
            SEntMan.EventBus.RaiseLocalEvent(plant, ref grow);
            Assert.That(SEntMan.GetComponent<PlantHolderComponent>(plant).Dead, Is.False);
            Assert.That(FindTendons(), Is.Empty);

            trays.AdjustWeed(tray, trait.WeedLevelThreshold - trayComponent.WeedLevel);
            SEntMan.EventBus.RaiseLocalEvent(plant, ref grow);
            var tendons = FindTendons();
            Assert.That(tendons, Has.Count.EqualTo(1));
            Assert.That(SEntMan.GetComponent<PlantHolderComponent>(plant).Dead, Is.True);
            Assert.That(SEntMan.HasComponent<PlantTraitKudzuComponent>(plant), Is.False);

            SEntMan.EventBus.RaiseLocalEvent(plant, ref grow);
            Assert.That(FindTendons(), Is.EquivalentTo(tendons), "The consumed mutation must not spawn twice.");

            foreach (var uid in tendons)
                SEntMan.DeleteEntity(uid);
            SEntMan.DeleteEntity(plant);
            SEntMan.DeleteEntity(tray);

            List<EntityUid> FindTendons()
            {
                var result = new List<EntityUid>();
                var query = SEntMan.EntityQueryEnumerator<BiomorphFleshKudzuComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out var transform))
                {
                    if (transform.GridUid == map.GridCoords.EntityId)
                        result.Add(uid);
                }

                return result;
            }
        });
    }
}
