using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Dropship;

[TestFixture]
[TestOf(typeof(SharedDropshipWeaponSystem))]
public sealed class RMCFlarePackSpawnTest : GameTest
{
    private static readonly EntProtoId FilledSignalFlarePack = "RMCPackFlareCAS";

    [Test]
    public async Task FilledSignalFlarePack_ClientMapInit_FillsEverySlot()
    {
        await Client.WaitAssertion(() =>
        {
            var pack = CSpawn(FilledSignalFlarePack);
            var slots = CComp<ItemSlotsComponent>(pack);

            Assert.That(slots.Slots, Has.Count.EqualTo(8));
            foreach (var slot in slots.Slots.Values)
            {
                Assert.That(slot.Item, Is.Not.Null);
                Assert.That(CEntMan.HasComponent<FlareSignalComponent>(slot.Item.Value), Is.True);
            }
        });
    }
}
