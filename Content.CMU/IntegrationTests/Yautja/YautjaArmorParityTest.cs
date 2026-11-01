using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaArmorParityTest : GameTest
{
    // cmss13 powered armor has BLOCK_KNOCKDOWN, a pounce into its front bounces off
    [Test]
    public async Task MilitaryCastePowerArmorWithstandsPounces()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var armor = SSpawnAtPosition("CMUYautjaPoweredArmor", map.GridCoords);

            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(human, armor, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(SEntMan.TryGetComponent(human, out RMCLeapProtectionComponent? protection), Is.True);
            Assert.That(protection!.ProtectionProviders, Does.Contain(armor));

            Assert.That(inventory.TryUnequip(human, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(SEntMan.TryGetComponent(human, out protection) && protection!.ProtectionProviders.Contains(armor), Is.False);
        });
    }
}
