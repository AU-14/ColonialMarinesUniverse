using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Equipment;

[TestFixture]
public sealed class FactionVendorBootsTest : GameTest
{
    [TestCase("AU14BootsJungleFilled")]
    [TestCase("AU14LACNBootsFilled")]
    [TestCase("CMULACNBootsWhiteFilled")]
    [TestCase("CMUBootsCorporateFilled")]
    [TestCase("CMUSpikeBootsFilled")]
    public async Task FilledBootsStartWithAKnife(string boots)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(boots, map.GridCoords);
            var knife = Server.System<ItemSlotsSystem>().GetItemOrNull(uid, "item");
            Assert.That(knife, Is.Not.Null, $"{boots} must start with a knife in its boot slot");
            Assert.That(Server.System<TagSystem>().HasTag(knife!.Value, "Knife"), Is.True);
        });
    }
}
