using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Storage;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUServerLogStorageFillTest : GameTest
{
    [Test]
    public async Task VonBandolierMedkitContainsItsFullLoadout()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pouch = SEntMan.SpawnAtPosition("RMCPouchMedkitVonBandolier", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(pouch);
            var prototypes = storage.StoredItems.Keys
                .Select(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
                .ToArray();
            Assert.That(prototypes, Is.EquivalentTo(new[]
            {
                "AU14HemostaticGauzePacket",
                "AU14Tourniquet",
                "AU14Tourniquet",
                "CMPacketPillsTricordrazine",
                "AU14PacketPillsTramadol",
                "CMUSplintItem",
                "CMUSplintItem",
            }));
        });
    }
}
