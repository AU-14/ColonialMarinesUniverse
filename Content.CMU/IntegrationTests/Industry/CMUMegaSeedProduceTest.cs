using Content.IntegrationTests.Fixtures;
using Content.Shared.Botany.Events;
using Content.Shared.CMU14.ColonyEconomy;

namespace Content.IntegrationTests.CMU14.Industry;

[TestFixture]
public sealed class CMUMegaSeedProduceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task OnlyMegaSeedProduceHarvestedFromTrayBecomesSellable()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tray = SEntMan.SpawnEntity("HydroponicsTrayEmpty", map.GridCoords);
            var trayPlant = SEntMan.SpawnEntity("ApplePlants", map.GridCoords);
            SEntMan.System<SharedTransformSystem>().SetParent(trayPlant, tray);

            var sellable = SEntMan.SpawnEntity("FoodApple", map.GridCoords);
            var megaSeedEvent = new ProduceSpawnedEvent(trayPlant, "AppleSeeds");
            SEntMan.EventBus.RaiseLocalEvent(sellable, ref megaSeedEvent);
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(sellable), Is.True);

            var unsupported = SEntMan.SpawnEntity("FoodApple", map.GridCoords);
            var unsupportedSeedEvent = new ProduceSpawnedEvent(trayPlant, "CannabisSeeds");
            SEntMan.EventBus.RaiseLocalEvent(unsupported, ref unsupportedSeedEvent);
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(unsupported), Is.False);

            var detachedPlant = SEntMan.SpawnEntity("ApplePlants", map.GridCoords);
            var detachedProduce = SEntMan.SpawnEntity("FoodApple", map.GridCoords);
            var detachedEvent = new ProduceSpawnedEvent(detachedPlant, "AppleSeeds");
            SEntMan.EventBus.RaiseLocalEvent(detachedProduce, ref detachedEvent);
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(detachedProduce), Is.False);

            SEntMan.DeleteEntity(tray);
            SEntMan.DeleteEntity(sellable);
            SEntMan.DeleteEntity(unsupported);
            SEntMan.DeleteEntity(detachedPlant);
            SEntMan.DeleteEntity(detachedProduce);
        });
    }
}
