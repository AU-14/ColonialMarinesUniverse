using Content.IntegrationTests.Fixtures;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Storage;

[TestFixture]
public sealed class ProdigyMedicalPouchTest : GameTest
{
    [Test]
    public async Task FilledPouchRetainsItsEntireLoadout()
    {
        var map = await Pair.CreateTestMap();
        EntityUid pouch = default;
        NetEntity netPouch = default;
        await Server.WaitAssertion(() =>
        {
            pouch = SEntMan.SpawnEntity("AU14PouchMedicalProdigyFilled", map.GridCoords);
            netPouch = SEntMan.GetNetEntity(pouch);
            AssertContents(SEntMan, pouch);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertContents(CEntMan, CEntMan.GetEntity(netPouch)));
        await Server.WaitPost(() => SEntMan.DeleteEntity(pouch));
    }

    private static void AssertContents(IEntityManager entities, EntityUid pouch)
    {
        var storage = entities.GetComponent<StorageComponent>(pouch);
        var prototypes = storage.Container.ContainedEntities
            .Select(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID).ToArray();
        Assert.That(prototypes, Is.EquivalentTo(new[]
        {
            "CMHealthAnalyzer", "CMTraumaKit10", "CMBurnKit10", "CMBloodPackFull", "CMSynthGraft",
            "CMSurgicalLine", "CMSurgicalCaseFilled", "AU14CMDefibrillator", "CMPortableSurgicalBedSpawnFolded",
            "AU14Tourniquet", "CMUCastItem", "CMUSplintItem", "CMUSplintItem",
        }));
    }
}
