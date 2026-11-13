#pragma warning disable RA0002
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-201: a young Totem pays through the real once-a-second update, a Bad Blood can read what it pays, and the
// armory window follows the income without anyone buying
[TestFixture]
public sealed class TotemIncomeLoopTest : GameTest
{
    private const string Totem = "CMUBadBloodTotem";
    private const string Armory = "CMUBadBloodCampArmory";
    private const string Human = "CMMobHuman";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task YoungTotemPaysThroughTheUpdateLoopAndSaysSo()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30, 33);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        FixedPoint4 before = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out _), Is.True);

                totem = Server.EntMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0)));
                spawned.Add(totem);

                var comp = Server.EntMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.That(comp.Registered, Is.True);
                Assert.That(comp.PaidIncome, Is.EqualTo(FixedPoint4.Zero));
                before = rules.GetSp();
            });

            // nothing but the systems' own Update: 0.2 SP a minute in 0.01 steps is a step every 3 seconds
            await Pair.RunSeconds(40);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                var loc = Server.ResolveDependency<ILocalizationManager>();
                var comp = Server.EntMan.GetComponent<BadBloodTotemComponent>(totem);
                var ent = new Entity<BadBloodTotemComponent>(totem, comp);
                var paid = rules.GetSp() - before;

                Assert.Multiple(() =>
                {
                    Assert.That(comp.Contested, Is.False);
                    Assert.That(paid, Is.GreaterThanOrEqualTo(FixedPoint4.New(0.08)),
                        "a young Totem pays several steps in 40 seconds of the real update loop");
                    Assert.That(paid, Is.LessThanOrEqualTo(FixedPoint4.New(0.2)));
                    Assert.That(comp.PaidIncome, Is.EqualTo(paid), "the Totem remembers what it has paid");
                });

                var text = totems.GetTotemExamineText(ent);
                Assert.That(text, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain(loc.GetString("cmu-bad-blood-totem-stage-young")));
                    Assert.That(text, Does.Contain("0.20"), "the examine shows the rate a minute");
                    Assert.That(text, Does.Contain(BadBloodCampSystem.FormatSp(comp.PaidIncome)),
                        "the examine shows what the Totem has paid");
                    Assert.That(text, Does.Not.Contain(loc.GetString("cmu-bad-blood-totem-examine-contested")));
                });

                Assert.That(BadBloodCampSystem.FormatSp(FixedPoint4.New(1.239)), Is.EqualTo("1.23"),
                    "SP is shown to two decimals and never rounded up");

                // a marine walks up
                var marine = Server.EntMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(33, 0)));
                spawned.Add(marine);
                Server.EntMan.EnsureComponent<MarineComponent>(marine);
                Assert.That(totems.GetTotemExamineText(ent),
                    Does.Contain(loc.GetString("cmu-bad-blood-totem-examine-contested")),
                    "the examine says when the Totem is contested");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task ArmoryBalanceFollowsTotemIncomeWithoutAPurchase()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map, 0, 30);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        var pointsType = string.Empty;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();

                // no Lodge stands, so the once-a-second Lodge tick is not what keeps the window current here
                var armory = entMan.SpawnEntity(Armory, map.GridCoords);
                spawned.Add(armory);
                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory, entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                pointsType = armoryEnt.Comp.PointsType;

                Assert.That(rules.AddSp(FixedPoint4.New(0.99)), Is.True);
                camp.PrepareArmoryBuyer(armoryEnt, grunt);
                Assert.That(entMan.GetComponent<CMVendorUserComponent>(grunt).ExtraPoints?.GetValueOrDefault(pointsType),
                    Is.EqualTo(0), "0.99 SP is no whole point yet");

                spawned.Add(entMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30, 0))));
            });

            await Pair.RunSeconds(30);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.GetSp(), Is.GreaterThanOrEqualTo(FixedPoint4.New(1)));
                Assert.That(Server.EntMan.GetComponent<CMVendorUserComponent>(grunt).ExtraPoints?.GetValueOrDefault(pointsType),
                    Is.EqualTo(1), "the vendor balance follows a Totem payout without a purchase");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // the Totem is a structure and needs floor to stand on
    private async Task LayFloor(TestMapData map, params int[] columns)
    {
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            foreach (var x in columns)
            {
                maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(x, 0), map.Tile.Tile);
            }
        });
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in spawned.Concat(party.All))
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
