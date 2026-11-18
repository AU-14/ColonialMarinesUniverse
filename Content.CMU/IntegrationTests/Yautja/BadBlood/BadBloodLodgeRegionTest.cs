using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Destructible;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-606: once the marines land, the 30-tile square the Lodge stands in is boxed on the marine tactical map and ARES
// names it. A new or moved Lodge moves the box, a fallen one takes it away, and the box never reveals the Lodge's blip
[TestFixture]
public sealed class BadBloodLodgeRegionTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const int RegionSize = 30;

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

#pragma warning disable RA0002 // seeds the area grid's colours and reads the map's shared lines directly
    [Test]
    public async Task TheLodgeSquareIsBoxedOnTheMarineMapAfterTheLanding()
    {
        var map = await Pair.CreateTestMap();

        // the box is drawn on the tactical map of the Lodge's grid, sized by its area grid. floor everything this test
        // spawns on: an anchored Lodge off-tile is detached from the grid
        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            var areas = entMan.EnsureComponent<AreaGridComponent>(map.Grid);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            for (var y = -1; y <= 4; y++)
            {
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
                areas.Colors[new Vector2i(x, y)] = Color.Gray;
            }
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1, startLanded: false);
        var spawned = new List<EntityUid>();
        EntityUid lodge = default;
        var announced = 0;

        try
        {
            await Server.WaitPost(() =>
            {
                announced = Server.System<BadBloodCampSystem>().LodgeRegionAnnounced;
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords.Offset(new Vector2(0, 3)));
                spawned.Add(lodge);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var camp = Server.System<BadBloodCampSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.GetLodge(), Is.EqualTo(lodge));
                Assert.That(rules.HaveMarinesLanded(), Is.False, "the marines are still in orbit");
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.LodgeRegionSize, Is.EqualTo(RegionSize), "the YAML default");

                Assert.Multiple(() =>
                {
                    Assert.That(RegionLines(map), Is.Empty, "no box before the marines land");
                    Assert.That(rule.Comp.LodgeRegionCorner, Is.Null);
                    Assert.That(camp.LodgeRegionAnnounced, Is.EqualTo(announced), "and ARES says nothing");
                });

                Assert.That(rules.MarkMarinesLanded(), Is.True);
                AssertBoxed(map, lodge, "the landing boxes the Lodge's square");
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.LodgeRegionCorner, Is.EqualTo(Vector2i.Zero), "tile (0,3) is in the square at (0,0)");
                    Assert.That(camp.LodgeRegionAnnounced, Is.EqualTo(announced + 1), "ARES names the square once");
                    Assert.That(camp.IsLodgeRevealed(), Is.False, "the box does not reveal the Lodge itself");
                    Assert.That(Server.EntMan.HasComponent<TacticalMapAlwaysVisibleComponent>(lodge), Is.False);
                });
            });

            // the Lodge's own update keeps the box as it is
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                AssertBoxed(map, lodge, "the box stays while the Lodge stands");
                Assert.That(Server.System<BadBloodCampSystem>().LodgeRegionAnnounced, Is.EqualTo(announced + 1),
                    "a Lodge that has not moved is not announced again");

                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(RegionLines(map), Is.Empty, "the box goes with the Lodge");
                    Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule) && rule.Comp.LodgeRegionCorner == null,
                        Is.True);
                });

                // a Lodge pitched after the landing, in the square to the west
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords.Offset(new Vector2(-3, 3)));
                spawned.Add(lodge);
                AssertBoxed(map, lodge, "a Lodge pitched after the landing is boxed at once");
                Assert.Multiple(() =>
                {
                    Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                    Assert.That(rule.Comp.LodgeRegionCorner, Is.EqualTo(new Vector2i(-RegionSize, 0)));
                    Assert.That(Server.System<BadBloodCampSystem>().LodgeRegionAnnounced, Is.EqualTo(announced + 2));
                });

                // and moved back east, into the first square
                Server.System<SharedTransformSystem>().SetCoordinates(lodge, map.GridCoords.Offset(new Vector2(2, 3)));
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                AssertBoxed(map, lodge, "a moved Lodge moves the box, leaving no box behind");
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.LodgeRegionCorner, Is.EqualTo(Vector2i.Zero));
                    Assert.That(Server.System<BadBloodCampSystem>().LodgeRegionAnnounced, Is.EqualTo(announced + 3));
                    Assert.That(Server.System<BadBloodCampSystem>().IsLodgeRevealed(), Is.False);
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in party.All.Concat(spawned))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    // the Lodge region's strokes on the test grid's tactical map. call on the server thread
    private List<TacticalMapLine> RegionLines(TestMapData map)
    {
        var tactical = Server.EntMan.GetComponent<TacticalMapComponent>(map.Grid);
        return tactical.SharedLines.Where(l => l.Color == BadBloodCampSystem.LodgeRegionColor).ToList();
    }

    // exactly one box of the region's colour, 30 tiles a side, with the Lodge's tile inside it. call on the server thread
    private void AssertBoxed(TestMapData map, EntityUid lodge, string message)
    {
        var lines = RegionLines(map);
        Assert.That(lines, Has.Count.EqualTo(4), message);

        var xs = lines.SelectMany(l => new[] { l.Start.X, l.End.X }).ToList();
        var ys = lines.SelectMany(l => new[] { l.Start.Y, l.End.Y }).ToList();

        // where the Lodge's tile centre lands on the classic canvas, the same way the box was placed
        var areas = Server.EntMan.GetComponent<AreaGridComponent>(map.Grid);
        Assert.That(areas.Colors, Is.Not.Empty, "the area grid sizes the canvas");
        var min = Vector2i.Zero;
        var max = Vector2i.Zero;
        foreach (var tile in areas.Colors.Keys)
        {
            min = Vector2i.ComponentMin(min, tile);
            max = Vector2i.ComponentMax(max, tile);
        }

        var lodgeTile = Server.System<SharedTransformSystem>().GetGridOrMapTilePosition(lodge);
        var at = CMUReconDrawingCoordinates.ToCanvas(new Vector2(lodgeTile.X + 0.5f, lodgeTile.Y + 0.5f), min, max);

        Assert.Multiple(() =>
        {
            Assert.That(xs.Max() - xs.Min(), Is.EqualTo(RegionSize * 3), $"{message}: 30 tiles wide at 3 pixels a tile");
            Assert.That(ys.Max() - ys.Min(), Is.EqualTo(RegionSize * 3), $"{message}: and 30 tall");
            Assert.That(at.X, Is.InRange((float) xs.Min(), (float) xs.Max()), $"{message}: the Lodge is inside the box");
            Assert.That(at.Y, Is.InRange((float) ys.Min(), (float) ys.Max()), $"{message}: the Lodge is inside the box");
        });
    }
#pragma warning restore RA0002
}
