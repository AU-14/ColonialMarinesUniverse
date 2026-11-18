using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 FLOW-704: the Hunt Order site picker. Every site it returns is open floor that passes the Totem site rules,
// lies in the requested band around the centre, keeps its distance from open orders, and is on the centre's own map.
// With no ground to stand on it returns nothing and does not throw.
[TestFixture]
public sealed class BadBloodHuntOrderSiteTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task SitesPassTheRulesStayInTheBandAndOnTheCentresMap()
    {
        var map = await Pair.CreateTestMap();
        await LayBlocks(map);
        var other = await Pair.CreateTestMap();
        await LayBlocks(other);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.HuntOrdersEnabled = false;
                var center = map.MapCoords;
                var sites = new List<MapCoordinates>();

                foreach (var near in new[] { true, false })
                {
                    var min = near ? rule.Comp.OrderNearMin : rule.Comp.OrderFarMin;
                    var max = near ? rule.Comp.OrderNearMax : rule.Comp.OrderFarMax;
                    for (var attempt = 0; attempt < 5; attempt++)
                    {
                        Assert.That(orders.TryPickSites(rule, center, near, sites), Is.True, $"ground in the {(near ? "near" : "far")} band");
                        Assert.That(sites, Has.Count.InRange(1, 2));
                        foreach (var site in sites)
                        {
                            Assert.Multiple(() =>
                            {
                                Assert.That(site.MapId, Is.EqualTo(map.MapId), "never on another z-level");
                                Assert.That((site.Position - center.Position).Length(), Is.InRange(min, max));
                                Assert.That(totems.GetSiteDenial(site, rule.Comp.OrderSpacing, rule.Comp.OrderLandingZoneDistance),
                                    Is.Null, "every site passes the Totem site rules");
                            });
                        }
                    }
                }

                // an open order pushes the next sites away from it
                Assert.That(orders.TryPickSites(rule, center, true, sites), Is.True);
                var first = orders.IssueOrderAt(sites[0], true);
                Assert.That(first, Is.Not.Null);
                spawned.Add(first!.Value);
                var firstAt = Server.System<SharedTransformSystem>().GetMapCoordinates(first.Value);
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    if (!orders.TryPickSites(rule, center, false, sites))
                        continue;

                    Assert.That(sites.All(s => (s.Position - firstAt.Position).Length() >= rule.Comp.OrderSpacing), Is.True,
                        "a new site keeps its distance from an open order");
                }
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task NoGroundMeansNoSiteAndNoThrow()
    {
        // the test map has one tile, at the centre: nothing in any band
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var orders = Server.System<BadBloodHuntOrderSystem>();
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var sites = new List<MapCoordinates>();

                Assert.DoesNotThrow(() =>
                {
                    Assert.That(orders.TryPickSites(rule, map.MapCoords, true, sites), Is.False, "no floor in the band, even widened");
                    Assert.That(sites, Is.Empty);
                    Assert.That(orders.TryPickSites(rule, map.MapCoords, false, sites), Is.False);
                    Assert.That(orders.TryPickSites(rule, MapCoordinates.Nullspace, true, sites), Is.False, "nullspace is no centre");
                    Assert.That(orders.TryPickSites(rule, new MapCoordinates(Vector2.Zero, new MapId(987654)), true, sites), Is.False,
                        "a map that does not exist is no centre");
                });
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

    // floor covering most of the near and the far band, east and west of the centre
    private async Task LayBlocks(TestMapData map)
    {
        await Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
            for (var y = -15; y <= 15; y++)
            {
                for (var x = 26; x <= 60; x++)
                {
                    tiles.Add((map.Tile.GridIndices + new Vector2i(x, y), map.Tile.Tile));
                    tiles.Add((map.Tile.GridIndices + new Vector2i(-x, y), map.Tile.Tile));
                }
            }

            Server.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
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
