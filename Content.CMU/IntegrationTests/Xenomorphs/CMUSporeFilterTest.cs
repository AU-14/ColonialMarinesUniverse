using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.CMU14.GasMask;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUSporeFilterTest : GameTest
{
    [Test]
    public async Task FilterWearsInCloudAndProtectsUntilStandingOnSource()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default, filter = default, cloud = default;
        float initial = 0;
        await Server.WaitAssertion(() =>
        {
            wearer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            filter = SEntMan.SpawnEntity("CMUGasMaskFilter", map.GridCoords);
            var mask = SEntMan.SpawnEntity("CMUMaskGas", map.GridCoords);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(mask, "filter", filter, null), Is.True);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, mask, "mask", force: true), Is.True);
            cloud = SEntMan.SpawnEntity("CMU14PathogenSporeCloud", map.GridCoords);
            var source = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<SharedMycotoxinSystem>().SetSporecaster(cloud, source);
            initial = SEntMan.GetComponent<GasMaskFilterComponent>(filter).Integrity;
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<GasMaskFilterComponent>(filter).Integrity, Is.InRange(1f, initial - 0.1f));
            Assert.That(SEntMan.HasComponent<MycotoxinExposureComponent>(wearer), Is.False);
            Assert.That(SEntMan.HasComponent<VictimInfectedComponent>(wearer), Is.False);
            var source = SEntMan.GetComponent<MycotoxinInjectorComponent>(cloud).Sporecaster!.Value;
            Server.System<SharedTransformSystem>().SetCoordinates(source, map.GridCoords);
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<VictimInfectedComponent>(wearer), Is.True));
    }
}
