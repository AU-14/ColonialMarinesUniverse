using Content.IntegrationTests.Fixtures;
using Content.Shared.Foldable;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Clothing;

public sealed class CMUTacticalWrapTest : GameTest
{
    [TestCase("mask")]
    [TestCase("neck")]
    public async Task RaisingAndLoweringWrapChangesVisibleIdentity(string slot)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var wearer = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var wrap = SSpawnAtPosition("RMCMaskScarfTanDown", map.GridCoords);
            var identity = Server.System<IdentitySystem>();
            var name = identity.GetEntityIdentity(wearer);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, wrap, slot, force: true), Is.True);
            Assert.That(identity.GetEntityIdentity(wearer), Is.EqualTo(name));
            var folding = Server.System<FoldableSystem>();
            folding.SetFolded(wrap, SComp<FoldableComponent>(wrap), false);
            Assert.That(identity.GetEntityIdentity(wearer), Is.Not.EqualTo(name));
            folding.SetFolded(wrap, SComp<FoldableComponent>(wrap), true);
            Assert.That(identity.GetEntityIdentity(wearer), Is.EqualTo(name));
        });
    }
}
