using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class RaiderLoadoutTest : GameTest
{
    [TestCase("CMUGearRaider", "rmc-aslot-rail", "RMCAttachmentRailFlashlight", "CMMagazineRifleM54CMK1", 0)]
    [TestCase("CMUGearRaiderLeader", "rmc-aslot-underbarrel", "RMCAttachmentFlashlightGrip", "CMMagazineRifleM54C", 2)]
    public async Task RaidersSpawnWithTheirFullKit(string gear, string lightSlot, string light, string magazine, int charges)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var raider = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.System<StationSpawningSystem>().EquipStartingGear(raider, gear);
            var inventory = Server.System<InventorySystem>();

            // Everything listed for the pack has to actually fit, starting storage drops what doesn't.
            Assert.That(inventory.TryGetSlotEntity(raider, "back", out var pack), Is.True);
            var packed = SEntMan.GetComponent<StorageComponent>(pack!.Value).Container.ContainedEntities
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype!.ID).ToList();
            Assert.That(packed.Count(id => id == magazine), Is.EqualTo(2));
            Assert.That(packed.Count(id => id == "CMGrenadeHighExplosive"), Is.EqualTo(2));
            Assert.That(packed.Count(id => id == "RMCExplosivePlastic"), Is.EqualTo(charges));
            Assert.That(packed, Is.SupersetOf(new[] { "CMCrowbar", "CMScrewdriver", "CMWirecutter", "CMMultitool" }));

            Assert.That(inventory.TryGetSlotEntity(raider, "suitstorage", out var rifle), Is.True);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable(
                (rifle!.Value, SEntMan.GetComponent<AttachableHolderComponent>(rifle.Value)), lightSlot, out var attached), Is.True);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(attached).EntityPrototype!.ID, Is.EqualTo(light));

            Assert.That(inventory.TryGetSlotEntity(raider, "pocket1", out var medical), Is.True);
            Assert.That(SEntMan.GetComponent<StorageComponent>(medical!.Value).Container.ContainedEntities, Has.Count.EqualTo(8));

            Assert.That(inventory.TryGetSlotEntity(raider, "pocket2", out var flares), Is.True);
            Assert.That(Server.System<SharedContainerSystem>().GetAllContainers(flares!.Value).Sum(c => c.ContainedEntities.Count), Is.GreaterThan(0));

            Assert.That(inventory.TryGetSlotEntity(raider, "outerClothing", out var armor), Is.True);
            Assert.That(SEntMan.GetComponent<CMArmorComponent>(armor!.Value).Melee, Is.EqualTo(25));
        });
    }
}
