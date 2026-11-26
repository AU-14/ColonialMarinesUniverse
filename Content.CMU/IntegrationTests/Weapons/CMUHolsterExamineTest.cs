using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;

namespace Content.IntegrationTests.CMU14.Weapons;

public sealed class CMUHolsterExamineTest : GameTest
{
    [Test]
    public async Task CoatConcealsShoulderHolsterAndSidearmPouchFromObservers()
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
        // The shoulder holster moves its starting pistol into the uniform on the next ticks.
        await Pair.RunTicksSync(3);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            // The client builds the first examine tooltip before the server response arrives.
            var uniform = CEntMan.GetEntity(uniformNet);
            Assert.That(CEntMan.GetComponent<CMHolsterComponent>(uniform).Concealable, Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var observer = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var inventory = Server.System<InventorySystem>();
            var examine = Server.System<ExamineSystemShared>();
            string Inspect(EntityUid viewer) => examine.GetExamineText(wearer, viewer).ToString();

            var pouch = SSpawnAtPosition("AU14PouchPistolAlt", map.GridCoords);
            var pistol = SSpawnAtPosition("CMWeaponPistolM1984", map.GridCoords);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(pouch, "pouch", pistol, null), Is.True);
            Assert.That(inventory.TryEquip(wearer, pouch, "pocket1"), Is.True);

            var rig = SSpawnAtPosition("RMCBeltHolsterPistol", map.GridCoords);
            var beltGun = SSpawnAtPosition("RMCWeaponPistolM77", map.GridCoords);
            Assert.That(Server.System<SharedStorageSystem>().Insert(rig, beltGun, out _), Is.True);
            Assert.That(inventory.TryEquip(wearer, rig, "belt"), Is.True);

            var shoulderGun = SComp<CMHolsterComponent>(SEntMan.GetEntity(uniformNet)).Contents.Single();
            var shoulderName = SComp<MetaDataComponent>(shoulderGun).EntityName;
            var pouchName = SComp<MetaDataComponent>(pistol).EntityName;
            var beltName = SComp<MetaDataComponent>(beltGun).EntityName;
            var vest = SSpawnAtPosition("AU14CivilianJacketGrayPufferVest", map.GridCoords);
            Assert.That(inventory.TryEquip(wearer, vest, "outerClothing"), Is.True);
            Assert.That(Inspect(observer), Does.Contain(shoulderName).And.Contain(pouchName));
            Assert.That(inventory.TryUnequip(wearer, "outerClothing"), Is.True);

            var coat = SSpawnAtPosition("AU14CivilianTanTrenchCoat", map.GridCoords);
            Assert.That(inventory.TryEquip(wearer, coat, "outerClothing"), Is.True);
            var covered = Inspect(observer);
            Assert.That(covered, Does.Not.Contain(shoulderName).And.Not.Contain(pouchName));
            Assert.That(covered, Does.Contain(beltName));
            Assert.That(Inspect(wearer), Does.Contain(shoulderName).And.Contain(pouchName));

            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(wearer, pistol), Is.True);
            Assert.That(Inspect(observer), Does.Contain(pouchName).And.Not.Contain(shoulderName));
            Assert.That(inventory.TryUnequip(wearer, "outerClothing"), Is.True);
            Assert.That(Inspect(observer), Does.Contain(shoulderName));
        });
    }
}
