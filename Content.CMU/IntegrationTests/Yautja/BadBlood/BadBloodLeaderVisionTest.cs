using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 FLOW-701 (D3): the honourable visor is the Engineer's alone. The Hunt Leader keeps the Bad Blood glasses and
// has the command sight instead; leadership moves, and the command sight follows it at once
[TestFixture]
public sealed class BadBloodLeaderVisionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TheLeaderHasCommandSightNotTheHonourableVisor()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -1; x <= 3; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rule = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rule.SetLeader(party.Leader), Is.True, "Test setup: the party's Leader leads");

                foreach (var body in party.All)
                {
                    Assert.That(Server.System<YautjaMaskSystem>().TryEnableWornVisor(body, feedback: false), Is.True,
                        "Test setup: the visor turns on");
                }

                Assert.Multiple(() =>
                {
                    Assert.That(Glasses(party.Leader), Is.EqualTo(Mask(party.Leader).Comp.BadBloodVisorGlassesPrototype.Id),
                        "the Leader keeps the Bad Blood sight");
                    Assert.That(Server.EntMan.HasComponent<BadBloodCommandSightComponent>(party.Leader), Is.True,
                        "the Leader has the command sight");
                    Assert.That(Glasses(grunt), Is.EqualTo(Mask(grunt).Comp.BadBloodVisorGlassesPrototype.Id),
                        "a grunt keeps the Bad Blood sight");
                });

                Assert.That(rule.SetLeader(grunt), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(Server.EntMan.HasComponent<BadBloodCommandSightComponent>(grunt), Is.True,
                        "the new Leader gains the command sight");
                    Assert.That(Server.EntMan.HasComponent<BadBloodCommandSightComponent>(party.Leader), Is.False,
                        "the deposed Leader loses it");
                    Assert.That(Glasses(grunt), Is.EqualTo(Mask(grunt).Comp.BadBloodVisorGlassesPrototype.Id),
                        "leading does not hand out the honourable visor");
                });

                // an Engineer still sees as an honourable hunter does
                Assert.That(Server.System<BadBloodCasteSystem>().TryTakeCaste(party.Leader, "Engineer"), Is.True);
                Assert.That(Glasses(party.Leader), Is.EqualTo(Mask(party.Leader).Comp.VisorGlassesPrototype.Id),
                    "the Engineer sees as an honourable hunter does");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in party.All)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private Entity<YautjaMaskComponent> Mask(EntityUid body)
    {
        Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(body, "mask", out var mask), Is.True,
            "Test setup: the Bad Blood loadout wears a mask");
        return (mask!.Value, Server.EntMan.GetComponent<YautjaMaskComponent>(mask.Value));
    }

    private string? Glasses(EntityUid body)
    {
        return Server.System<InventorySystem>().TryGetSlotEntity(body, "eyes", out var eyes)
            ? Server.EntMan.GetComponent<MetaDataComponent>(eyes.Value).EntityPrototype?.ID
            : null;
    }
}
