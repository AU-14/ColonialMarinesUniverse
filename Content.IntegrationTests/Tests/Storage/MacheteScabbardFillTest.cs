using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;

namespace Content.IntegrationTests.Tests.Storage;

[TestFixture]
[TestOf(typeof(ItemSlotsSystem))]
public sealed class MacheteScabbardFillTest : GameTest
{
    [Test]
    public async Task FilledScabbardSpawnsWithM2132Machete()
    {
        var map = await Pair.CreateTestMap();
        EntityUid scabbard = default;
        NetEntity scabbardNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                scabbard = SEntMan.SpawnEntity("RMCScabbardMacheteFilled", map.GridCoords);
                scabbardNet = SEntMan.GetNetEntity(scabbard);
                AssertFilledWith(SEntMan, scabbard, "CMM2132Machete");
            });

            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() => AssertFilledWith(SEntMan, scabbard, "CMM2132Machete"));
            await Client.WaitAssertion(() =>
                AssertFilledWith(CEntMan, CEntMan.GetEntity(scabbardNet), "CMM2132Machete"));
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(scabbard));
        }
    }

    private static void AssertFilledWith(IEntityManager entityManager, EntityUid scabbard, string expectedPrototype)
    {
        var slot = entityManager.GetComponent<ItemSlotsComponent>(scabbard).Slots["item"];

        Assert.That(slot.HasItem, Is.True,
            "RMCScabbardMacheteFilled must not be empty when spawned by a vendor");
        Assert.That(slot.Item, Is.Not.Null);
        Assert.That(entityManager.GetComponent<MetaDataComponent>(slot.Item.Value).EntityPrototype?.ID,
            Is.EqualTo(expectedPrototype));
    }
}
