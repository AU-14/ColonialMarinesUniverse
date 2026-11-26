using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Examine;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Weapons;

public sealed class CMUHolsterExamineTest : GameTest
{
    [Test]
    public async Task CoatConcealsPistolTransferredFromShoulderWebbingToUniform()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;
        NetEntity uniformNet = default;
        await Server.WaitAssertion(() =>
        {
            wearer = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, wearer);
            var uniform = SSpawnAtPosition("RMCJumpsuitMarineRoyalMK80", map.GridCoords);
            uniformNet = SEntMan.GetNetEntity(uniform);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, uniform, "jumpsuit"), Is.True);
        });

        // The shoulder webbing moves its pistol and holster onto the uniform after spawning.
        await Pair.RunTicksSync(3);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var uniform = CEntMan.GetEntity(uniformNet);
            Assert.That(CEntMan.GetComponent<CMHolsterComponent>(uniform).Concealable, Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var observer = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var pistol = SComp<CMHolsterComponent>(SEntMan.GetEntity(uniformNet)).Contents.Single();
            var name = SComp<MetaDataComponent>(pistol).EntityName;
            var examine = Server.System<ExamineSystemShared>();
            Assert.That(examine.GetExamineText(wearer, observer).ToString(), Does.Contain(name));

            var coat = SSpawnAtPosition("AU14CivilianTanTrenchCoat", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, coat, "outerClothing"), Is.True);
            Assert.That(examine.GetExamineText(wearer, observer).ToString(), Does.Not.Contain(name));
        });
    }
}
