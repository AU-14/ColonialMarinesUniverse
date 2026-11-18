using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 FLOW-705, FLOW-711: Bad Bloods alone on a Hunt Ground fill its meter, marines alone drain it, both together
// freeze it. Full, a registered, consecrated Totem rises there with a horn, the clan gets SP and each Bad Blood who held
// the ground gets Honour, once. Totem state changes play their sounds: one per contest, none while desecrated.
[TestFixture]
public sealed class BadBloodHuntGroundSeizeTest : GameTest
{
    private const int Column = 30;

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TheMeterFillsDrainsFreezesAndRaisesATotemOnce()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var (holder, helper) = (party.Grunts[0], party.Grunts[1]);
        var spawned = new List<EntityUid>();
        EntityUid ground = default;
        EntityUid marine = default;
        var progress = 0f;
        var sp = FixedPoint4.Zero;
        var honour = FixedPoint4.Zero;
        var leaderHonour = FixedPoint4.Zero;
        var starts = 0;
        var contests = 0;
        var horns = 0;
        var seized = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.HuntOrdersEnabled = false;

                var issued = orders.IssueOrderAt(At(map, 0));
                Assert.That(issued, Is.Not.Null);
                ground = issued!.Value;
                Server.EntMan.GetComponent<BadBloodHuntGroundComponent>(ground).SeizeTime = TimeSpan.FromSeconds(6);

                starts = orders.SeizeStartSoundsPlayed;
                contests = orders.ContestedSoundsPlayed;
                horns = orders.RisenSoundsPlayed;
                seized = orders.OrdersSeizedCount;
                Move(holder, map, 0);
            });

            // a Bad Blood alone fills it
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var hunt = Server.EntMan.GetComponent<BadBloodHuntGroundComponent>(ground);
                Assert.Multiple(() =>
                {
                    Assert.That(hunt.Progress, Is.GreaterThan(0f), "a Bad Blood alone fills the meter");
                    Assert.That(hunt.Filling, Is.True);
                    Assert.That(orders.SeizeStartSoundsPlayed, Is.EqualTo(starts + 1), "the seize start is heard once");
                });

                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(Column + 1, 0));
                spawned.Add(marine);
            });

            // a marine steps on: frozen
            await Pair.RunSeconds(1.2f);
            await Server.WaitAssertion(() =>
            {
                progress = Server.EntMan.GetComponent<BadBloodHuntGroundComponent>(ground).Progress;
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var hunt = Server.EntMan.GetComponent<BadBloodHuntGroundComponent>(ground);
                Assert.Multiple(() =>
                {
                    Assert.That(hunt.Contested, Is.True);
                    Assert.That(hunt.Progress, Is.EqualTo(progress).Within(0.0001f), "both sides on it freeze the meter");
                    Assert.That(orders.ContestedSoundsPlayed, Is.EqualTo(contests + 1), "one contested sound per contest");
                    Assert.That(orders.MarinePopupsSent, Is.GreaterThan(0), "the marine is told what it is");
                });

                // the Bad Blood walks off: the marine drains it
                Move(holder, map, -Column);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var hunt = Server.EntMan.GetComponent<BadBloodHuntGroundComponent>(ground);
                Assert.That(hunt.Progress, Is.LessThan(progress), "a marine alone drains the meter");

                // two Bad Bloods take it back and hold it
                Server.EntMan.DeleteEntity(marine);
                Move(holder, map, 0);
                Move(helper, map, 1);
                sp = rules.GetSp();
                honour = rules.GetHonour(holder);
                leaderHonour = rules.GetHonour(party.Leader);
            });

            await Pair.RunSeconds(6);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(entMan.EntityExists(ground), Is.False, "the marker goes when the Totem rises");
                Assert.That(rules.GetTotems(), Has.Count.EqualTo(1), "a Totem rises");
                var totem = rules.GetTotems()[0];
                spawned.Add(totem);
                var comp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                var at = Server.System<SharedTransformSystem>().GetMapCoordinates(totem);
                Assert.Multiple(() =>
                {
                    Assert.That((at.Position - At(map, 0).Position).Length(), Is.LessThan(1.5f), "on the ground that was seized");
                    Assert.That(comp.Registered, Is.True);
                    Assert.That(comp.Desecrated, Is.False, "it rises consecrated");
                    Assert.That((rules.GetSp() - sp).Double(), Is.EqualTo(rule.Comp.SeizeRewardSp.Double()).Within(0.05),
                        "the clan is paid for the seize");
                    Assert.That(rules.GetHonour(holder) - honour, Is.EqualTo(rule.Comp.SeizeRewardHonour), "Honour to who held it");
                    Assert.That(rules.GetHonour(helper), Is.GreaterThanOrEqualTo(rule.Comp.SeizeRewardHonour));
                    Assert.That(rules.GetHonour(party.Leader), Is.EqualTo(leaderHonour), "nothing to who was not there");
                    Assert.That(orders.OrdersSeizedCount, Is.EqualTo(seized + 1));
                    Assert.That(orders.RisenSoundsPlayed, Is.EqualTo(horns + 1), "one horn per seize");
                    Assert.That(orders.GetOpenOrders(), Is.Empty);
                    Assert.That(Server.System<BadBloodObjectiveTimelineSystem>().GetEntries()
                        .Any(e => e.Kind == BadBloodTimelineKind.HuntOrderSeized), Is.True, "the timeline logs it");
                });

                sp = rules.GetSp();
                honour = rules.GetHonour(holder);
            });

            // the reward is paid once
            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That((rules.GetSp() - sp).Double(), Is.LessThan(0.5), "only the young Totem's trickle, no second reward");
                    Assert.That(rules.GetHonour(holder), Is.EqualTo(honour));
                    Assert.That(orders.OrdersSeizedCount, Is.EqualTo(seized + 1));
                    Assert.That(orders.RisenSoundsPlayed, Is.EqualTo(horns + 1));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task TotemStateChangesPlayOneSoundEach()
    {
        var map = await Pair.CreateTestMap();
        await LayFloor(map);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        EntityUid marine = default;
        int contested = 0, freed = 0, desecrated = 0, reconsecrated = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                rule.Comp.HuntOrdersEnabled = false;

                var raised = totems.SpawnOrderedTotem(At(map, 0));
                Assert.That(raised, Is.Not.Null);
                totem = raised!.Value;
                spawned.Add(totem);

                contested = totems.ContestedSoundsPlayed;
                freed = totems.FreedSoundsPlayed;
                desecrated = totems.DesecratedSoundsPlayed;
                reconsecrated = totems.ReconsecratedSoundsPlayed;

                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(Column + 1, 0));
                spawned.Add(marine);
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var totems = Server.System<BadBloodTotemSystem>();
                var comp = Server.EntMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.That(comp.Contested, Is.True);
                Assert.That(totems.ContestedSoundsPlayed, Is.EqualTo(contested + 1), "one contested sound per contest, not per tick");

                totems.SetDesecrated((totem, comp), true);
                Assert.Multiple(() =>
                {
                    Assert.That(totems.DesecratedSoundsPlayed, Is.EqualTo(desecrated + 1));
                    Assert.That(totems.FreedSoundsPlayed, Is.EqualTo(freed), "a desecration is not a freed Totem");
                });
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                var totems = Server.System<BadBloodTotemSystem>();
                var comp = Server.EntMan.GetComponent<BadBloodTotemComponent>(totem);
                Assert.That(totems.ContestedSoundsPlayed, Is.EqualTo(contested + 1), "no contest sound while desecrated");

                totems.SetDesecrated((totem, comp), false);
                Assert.That(totems.ReconsecratedSoundsPlayed, Is.EqualTo(reconsecrated + 1));
            });

            // the marine is still there: a new contest, then the marine leaves and frees it
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var totems = Server.System<BadBloodTotemSystem>();
                Assert.That(totems.ContestedSoundsPlayed, Is.EqualTo(contested + 2));
                Server.EntMan.DeleteEntity(marine);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodTotemSystem>().FreedSoundsPlayed, Is.EqualTo(freed + 1),
                    "the Totem is heard coming back");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // the map position of the tile Column + offset tiles east of the grid origin. call on the server thread
    private MapCoordinates At(TestMapData map, int offset)
    {
        return Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords.Offset(new Vector2(Column + offset, 0)));
    }

    private void Move(EntityUid uid, TestMapData map, int offset)
    {
        Server.System<SharedTransformSystem>().SetCoordinates(uid, map.GridCoords.Offset(new Vector2(Column + offset, 0)));
    }

    // the Totem is a structure and needs floor to stand on
    private async Task LayFloor(TestMapData map)
    {
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -1; x <= 2; x++)
            {
                maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(Column + x, 0), map.Tile.Tile);
            }
        });
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await Server.WaitPost(() =>
        {
            foreach (var ground in Server.System<BadBloodHuntOrderSystem>().GetOpenOrders().ToList())
            {
                Server.EntMan.DeleteEntity(ground);
            }
        });

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
