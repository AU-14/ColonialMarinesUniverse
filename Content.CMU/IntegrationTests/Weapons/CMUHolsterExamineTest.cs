using System.Linq;
using Content.Client.Inventory;
using Content.Client.Strip;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Webbing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Strip;
using Content.Shared.Strip.Components;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.CMU14.Weapons;

public sealed class CMUHolsterExamineTest : GameTest
{
    [Test]
    public async Task StripMenuUsesHiddenPocketIconForCoveredHolsters()
    {
        const string hidden = StrippableBoundUserInterface.HiddenPocketEntityId;
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid uniform = default;
        NetEntity wearerNet = default;
        await Server.WaitAssertion(() =>
        {
            var observer = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, observer);
            wearer = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            wearerNet = SEntMan.GetNetEntity(wearer);
            var inventory = Server.System<InventorySystem>();
            void Equip(string id, string slot) =>
                Assert.That(inventory.TryEquip(wearer, SSpawnAtPosition(id, map.GridCoords), slot), Is.True);

            uniform = SSpawnAtPosition("JumpsuitMarine", map.GridCoords);
            Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit"), Is.True);
            Equip("AU14PouchPistolAlt", "pocket1");
            Equip("RMCBeltHolsterPistol", "belt");
            Equip("AU14CivilianTanTrenchCoat", "outerClothing");
            Assert.That(Server.System<SharedStrippableSystem>()
                .TryOpenStrippingUi(observer, (wearer, SComp<StrippableComponent>(wearer))), Is.True);
        });

        async Task AssertSlots(string uniformIcon, string pouchIcon)
        {
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<StrippingMenu>().Single();
                // Headless clients don't draw frames. Apply the same dirty-menu update as FrameUpdate.
                if (window.Dirty)
                {
                    window.Dirty = false;
                    Assert.That(Client.System<SharedUserInterfaceSystem>().TryGetOpenUi<StrippableBoundUserInterface>(
                        CEntMan.GetEntity(wearerNet), StrippingUiKey.Key, out var menu), Is.True);
                    menu!.UpdateMenu();
                }

                string? Icon(string slot)
                {
                    var button = window.InventoryContainer.Children.OfType<SlotButton>().Single(b => b.SlotName == slot);
                    return button.Entity is { } item ? CEntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID : null;
                }

                Assert.That(Icon("jumpsuit"), Is.EqualTo(uniformIcon));
                Assert.That(Icon("pocket1"), Is.EqualTo(pouchIcon));
                Assert.That(Icon("pocket2"), Is.Null);
                Assert.That(Icon("belt"), Is.EqualTo("RMCBeltHolsterPistol"));
            });
        }

        await AssertSlots("JumpsuitMarine", hidden);
        await Server.WaitAssertion(() =>
        {
            var holster = SSpawnAtPosition("CMWebbingHolster", map.GridCoords);
            Assert.That(Server.System<SharedWebbingSystem>().Attach((uniform, SComp<WebbingClothingComponent>(uniform)),
                holster, wearer, out _), Is.True);
        });
        await AssertSlots(hidden, hidden);
        await Server.WaitAssertion(() => Assert.That(Server.System<InventorySystem>().TryUnequip(wearer, "outerClothing"), Is.True));
        await AssertSlots("JumpsuitMarine", "AU14PouchPistolAlt");
    }

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
