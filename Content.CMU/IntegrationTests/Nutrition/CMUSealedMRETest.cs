using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Nutrition;

/// <summary>
///     A ration pack has to be ripped open before you can get into it. Covers the UA MRE and the faction rations
///     built on it.
/// </summary>
[TestFixture]
public sealed class CMUSealedMRETest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMMRE")]
    [TestCase("CMUMRELACN")]
    [TestCase("AU14MREUPP")]
    [TestCase("AU14MRETWERMC")]
    [TestCase("CMUMREFSR")]
    [TestCase("RMCMREWeYaPMC")]
    public async Task SealedUntilRipped(string ration)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var at = new MapCoordinates(Vector2.Zero, mapId);
            var user = SEntMan.SpawnEntity("MobHuman", at);
            var mre = SEntMan.SpawnEntity(ration, at);

            var storage = SEntMan.System<SharedStorageSystem>();
            var openable = SEntMan.System<OpenableSystem>();
            var storageComp = SEntMan.GetComponent<StorageComponent>(mre);

            Assert.That(openable.IsClosed(mre), Is.True, $"{ration} spawns already open");
            Assert.That(storage.CanInteract(user, (mre, storageComp)), Is.False, $"{ration} can be opened while sealed");

            openable.SetOpen(mre, true);
            Assert.That(storage.CanInteract(user, (mre, storageComp)), Is.True, $"{ration} stays shut after ripping it open");

            SEntMan.DeleteEntity(map);
        });
    }
}
