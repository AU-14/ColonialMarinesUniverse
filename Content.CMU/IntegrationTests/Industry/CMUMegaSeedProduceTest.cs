using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.CMU14.ColonyEconomy;

namespace Content.IntegrationTests.CMU14.Industry;

[TestFixture]
public sealed class CMUMegaSeedProduceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("RMCMangoPlants", true, true)]
    [TestCase("TobaccoPlants", true, false)]
    [TestCase("RMCMangoPlants", false, false)]
    public async Task OnlyMegaSeedProduceHarvestedFromTrayBecomesSellable(string plantId, bool inTray, bool sellable)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tray = SEntMan.SpawnEntity("HydroponicsTrayEmpty", map.GridCoords);
            var plant = SEntMan.SpawnEntity(plantId, map.GridCoords);
            if (inTray)
                SEntMan.System<SharedTransformSystem>().SetParent(plant, tray);

            var before = SEntMan.EntityQuery<ProduceComponent>().Select(p => p.Owner).ToHashSet();
            SEntMan.System<BotanySystem>().SpawnProduce(plant, map.GridCoords, 1);
            var harvest = SEntMan.EntityQuery<ProduceComponent>().Select(p => p.Owner).Single(uid => !before.Contains(uid));
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(harvest), Is.EqualTo(sellable));
            if (sellable)
            {
                Assert.That(SEntMan.System<CMUIndustryExportSystem>().TryGetCanonicalUnitPrice(harvest, out var unitPrice), Is.True);
                Assert.That(SEntMan.System<PricingSystem>().GetPrice(harvest), Is.EqualTo(unitPrice));
                Assert.That(SEntMan.System<CMUIndustryExportSystem>().CanExport(harvest), Is.True,
                    "Harvested food remains sellable even when it contains nutrients.");
            }

            SEntMan.DeleteEntity(harvest);
            SEntMan.DeleteEntity(plant);
            SEntMan.DeleteEntity(tray);
        });
    }
}
