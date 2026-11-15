using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Explosion;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU.Mining;

public sealed class CMUMagnetiteExplosionTest : GameTest
{
    [Test]
    public async Task PlasticExplosiveDropsMagnetiteOreOnlyOnce()
    {
        var map = await Pair.CreateTestMap();
        EntityUid deposit = default;
        var oreCount = 0;

        await Server.WaitAssertion(() =>
        {
            deposit = SEntMan.SpawnEntity("CMUWallMineableLV376Steel", map.GridCoords);
            var charge = SEntMan.SpawnEntity("RMCExplosivePlastic", map.GridCoords);

            Server.System<ExplosionSystem>().TriggerExplosive(charge);

            Assert.That(SEntMan.IsQueuedForDeletion(deposit), Is.True);
            oreCount = CountIronOre();
            Assert.That(oreCount, Is.InRange(1, 5), "Blasting magnetite must release its iron ore.");

            // Multiple charges can reach the same wall before queued deletion is processed.
            var secondCharge = SEntMan.SpawnEntity("RMCExplosiveBreachingCharge", map.GridCoords);
            var triggered = new CMExplosiveTriggeredEvent();
            SEntMan.EventBus.RaiseLocalEvent(secondCharge, ref triggered);
            Assert.That(CountIronOre(), Is.EqualTo(oreCount), "A queued wall must not drop ore again.");
        });

        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(deposit), Is.True);
            Assert.That(CountIronOre(), Is.EqualTo(oreCount), "Iron ore must survive the processed blast.");
        });

        int CountIronOre()
        {
            var count = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (metadata.EntityPrototype?.ID == "SteelOre1" && transform.MapUid == map.MapUid &&
                    !SEntMan.IsQueuedForDeletion(uid))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
