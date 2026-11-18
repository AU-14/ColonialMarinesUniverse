using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Dialog;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 FLOW-703, FLOW-706: the Clan Leader issues Hunt Orders on their own once a Lodge stands, never more than the
// clan's limit together with its Totems; a freed slot or a lapse waits the cooldown; no Lodge pauses them. The Hunt
// Leader picks one of two sites (else the first after the pick time) and may petition for another ground for SP.
[TestFixture]
public sealed class BadBloodHuntOrderTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string HuntGround = "CMUBadBloodHuntGround";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task OrdersFollowTheLodgeTheLimitTheCooldownAndLapse()
    {
        var map = await Pair.CreateTestMap();
        await LayGround(map);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid lodge = default;
        EntityUid ground = default;
        var issued = 0;
        var lapsed = 0;

        try
        {
            // no Lodge: no orders, and the first-order clock has not started
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var orders = Server.System<BadBloodHuntOrderSystem>();
                Assert.That(rule.Comp.NextOrderAt, Is.Null);
                Assert.That(orders.GetOpenOrders(), Is.Empty);
                issued = orders.OrdersIssuedCount;
                lapsed = orders.OrdersLapsedCount;

                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                spawned.Add(lodge);
            });

            // the first order is due OrderFirstDelay after the first Lodge
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(rule.Comp.NextOrderAt, Is.Not.Null);
                Assert.That(rule.Comp.NextOrderAt!.Value - now,
                    Is.GreaterThan(rule.Comp.OrderFirstDelay - TimeSpan.FromSeconds(4)).And.LessThanOrEqualTo(rule.Comp.OrderFirstDelay),
                    "the first order waits two minutes after the first Lodge");
                Assert.That(Server.System<BadBloodHuntOrderSystem>().GetOpenOrders(), Is.Empty);

                // skip the wait; the Hunt Leader's pick (if one is offered) times out at once
                rule.Comp.NextOrderAt = now;
                rule.Comp.OrderPickTime = TimeSpan.FromSeconds(1);
            });

            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(orders.GetOpenOrders(), Has.Count.EqualTo(1), "one order is issued");
                ground = orders.GetOpenOrders()[0];
                Assert.That(Server.EntMan.GetComponent<MetaDataComponent>(ground).EntityPrototype?.ID, Is.EqualTo(HuntGround));

                var distance = (Server.System<SharedTransformSystem>().GetMapCoordinates(ground).Position - map.MapCoords.Position).Length();
                Assert.Multiple(() =>
                {
                    Assert.That(orders.OrdersIssuedCount, Is.EqualTo(issued + 1));
                    Assert.That(distance, Is.InRange(rule.Comp.OrderNearMin, rule.Comp.OrderNearMax), "the first order is in the near band");
                    Assert.That(rule.Comp.NextOrderAt, Is.GreaterThan(now + rule.Comp.OrderCooldown - TimeSpan.FromSeconds(5)),
                        "the next order waits the cooldown");
                    Assert.That(Server.System<BadBloodObjectiveTimelineSystem>().GetEntries()
                        .Any(e => e.Kind == BadBloodTimelineKind.HuntOrderIssued), Is.True, "the timeline logs it");
                });

                // two more Totems fill the clan's three slots with the open order
                var rules = Server.System<BadBloodCampRuleSystem>();
                for (var i = 0; i < 2; i++)
                {
                    var fake = Server.EntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(-80 - 30 * i, 0)));
                    spawned.Add(fake);
                    Assert.That(rules.TryRegisterTotem(fake), Is.True);
                }

                Assert.That(orders.CountActive(), Is.EqualTo(3));
                rule.Comp.NextOrderAt = now;
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodHuntOrderSystem>().GetOpenOrders(), Has.Count.EqualTo(1),
                    "open orders and Totems never pass three");

                // a Totem falls: the slot frees and the next order waits the cooldown from now
                Server.EntMan.DeleteEntity(spawned[^1]);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(rule.Comp.NextOrderAt, Is.GreaterThan(now + rule.Comp.OrderCooldown - TimeSpan.FromSeconds(5)),
                    "a freed slot starts the cooldown");
                Assert.That(Server.System<BadBloodHuntOrderSystem>().GetOpenOrders(), Has.Count.EqualTo(1));

                // nobody stands on the ground when it runs out
                Server.EntMan.GetComponent<BadBloodHuntGroundComponent>(ground).ExpiresAt = now;
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var now = Server.ResolveDependency<IGameTiming>().CurTime;
                Assert.Multiple(() =>
                {
                    Assert.That(Server.EntMan.EntityExists(ground), Is.False, "a lapsed ground goes");
                    Assert.That(orders.GetOpenOrders(), Is.Empty);
                    Assert.That(orders.OrdersLapsedCount, Is.EqualTo(lapsed + 1));
                    Assert.That(rule.Comp.NextOrderAt, Is.GreaterThan(now + rule.Comp.OrderCooldown - TimeSpan.FromSeconds(5)),
                        "a lapse waits the cooldown before the next order");
                    Assert.That(Server.System<BadBloodObjectiveTimelineSystem>().GetEntries()
                        .Any(e => e.Kind == BadBloodTimelineKind.HuntOrderLapsed), Is.True);
                });

                rule.Comp.NextOrderAt = now;
            });

            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                var orders = Server.System<BadBloodHuntOrderSystem>();
                Assert.That(orders.GetOpenOrders(), Has.Count.EqualTo(1), "after a lapse the order is issued again");

                // no Lodge: nothing new is issued
                Server.EntMan.DeleteEntity(orders.GetOpenOrders()[0]);
                Server.EntMan.DeleteEntity(lodge);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                rule.Comp.NextOrderAt = Server.ResolveDependency<IGameTiming>().CurTime;
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().HasLodge(), Is.False);
                Assert.That(Server.System<BadBloodHuntOrderSystem>().GetOpenOrders(), Is.Empty, "orders pause without a Lodge");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task TheHuntLeaderPicksOneOfTwoSitesOrTheFirstIsTaken()
    {
        var map = await Pair.CreateTestMap();
        await LayGround(map);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var pickId = 0;
        var offered = 0;
        var sites = new List<MapCoordinates>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords));
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                rule.Comp.NextOrderAt = Server.ResolveDependency<IGameTiming>().CurTime;
                offered = Server.System<BadBloodHuntOrderSystem>().PicksOffered;
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var orders = Server.System<BadBloodHuntOrderSystem>();
                Assert.That(rule.Comp.PendingPick, Is.Not.Null, "the living Hunt Leader is offered the order first");
                var pick = rule.Comp.PendingPick!;
                Assert.Multiple(() =>
                {
                    Assert.That(pick.Leader, Is.EqualTo(party.Leader));
                    Assert.That(pick.Sites, Has.Count.EqualTo(2), "two sites to choose from");
                    Assert.That(orders.PicksOffered, Is.EqualTo(offered + 1));
                    Assert.That(orders.GetOpenOrders(), Is.Empty, "nothing is issued before the pick");
                    Assert.That(orders.CountActive(), Is.EqualTo(1), "the pick holds a slot");
                    Assert.That(Server.EntMan.HasComponent<DialogComponent>(party.Leader), Is.True, "the Hunt Leader gets the dialog");
                });

                pickId = pick.Id;
                sites.AddRange(pick.Sites);
                Assert.That(orders.ChooseSite(party.Grunts[0], pickId, 1), Is.False, "only the Hunt Leader picks");
                Assert.That(orders.ChooseSite(party.Leader, pickId + 1, 1), Is.False, "a stale dialog picks nothing");
                Assert.That(orders.ChooseSite(party.Leader, pickId, 1), Is.True);

                Assert.That(rule.Comp.PendingPick, Is.Null);
                Assert.That(orders.GetOpenOrders(), Has.Count.EqualTo(1));
                AssertAt(orders.GetOpenOrders()[0], sites[1], "the order stands where the Hunt Leader chose");
                Assert.That(orders.ChooseSite(party.Leader, pickId, 0), Is.False, "a pick is made once");

                Server.EntMan.DeleteEntity(orders.GetOpenOrders()[0]);
            });

            // the freed slot starts the cooldown; skip it, and give the Hunt Leader two seconds this time
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                rule.Comp.OrderPickTime = TimeSpan.FromSeconds(2);
                rule.Comp.NextOrderAt = Server.ResolveDependency<IGameTiming>().CurTime;
            });

            await Pair.RunSeconds(1.5f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.PendingPick, Is.Not.Null);
                Assert.That(rule.Comp.PendingPick!.Id, Is.Not.EqualTo(pickId), "a new offer");
                sites.Clear();
                sites.AddRange(rule.Comp.PendingPick.Sites);
            });

            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().TryGetRule(out var rule), Is.True);
                var orders = Server.System<BadBloodHuntOrderSystem>();
                Assert.That(rule.Comp.PendingPick, Is.Null, "the pick times out");
                Assert.That(orders.GetOpenOrders(), Has.Count.EqualTo(1));
                AssertAt(orders.GetOpenOrders()[0], sites[0], "without a choice the first site is taken");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task APetitionRerollsAnUnstartedOrderForSpOncePerCooldown()
    {
        var map = await Pair.CreateTestMap();
        await LayGround(map);
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var orders = Server.System<BadBloodHuntOrderSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.HuntOrdersEnabled = false;
                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));

                // the Hunt Leader, and only they, carry the petition
                Assert.That(entMan.TryGetComponent(party.Leader, out BadBloodHuntPetitionComponent? petition), Is.True);
                Assert.That(petition!.ActionEntity, Is.Not.Null, "the Hunt Leader holds Petition the Clan Leader");
                Assert.That(entMan.HasComponent<BadBloodHuntPetitionComponent>(party.Grunts[0]), Is.False);

                var oldSite = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords.Offset(new Vector2(35, 0)));
                var old = orders.IssueOrderAt(oldSite);
                Assert.That(old, Is.Not.Null);
                Assert.That(rules.AddSp(FixedPoint4.New(10)), Is.True);
                var sp = rules.GetSp();

                Assert.That(orders.TryPetition(party.Grunts[0]), Is.False, "only the Hunt Leader petitions");
                Assert.That(rules.GetSp(), Is.EqualTo(sp));

                Assert.That(orders.TryPetition(party.Leader), Is.True);
                Assert.That(orders.GetOpenOrders(), Has.Count.EqualTo(1));
                var fresh = orders.GetOpenOrders()[0];
                var freshAt = Server.System<SharedTransformSystem>().GetMapCoordinates(fresh);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(old!.Value), Is.False, "the old ground is withdrawn");
                    Assert.That(fresh, Is.Not.EqualTo(old!.Value));
                    Assert.That(rules.GetSp(), Is.EqualTo(sp - rule.Comp.TotemCost), "a petition costs the Totem price");
                    Assert.That((freshAt.Position - oldSite.Position).Length(), Is.GreaterThanOrEqualTo(rule.Comp.OrderSpacing),
                        "the new ground is somewhere else");
                    Assert.That(orders.PetitionsGranted, Is.GreaterThan(0));
                });

                Assert.That(orders.TryPetition(party.Leader), Is.False, "one petition per cooldown");
                Assert.That(rules.GetSp(), Is.EqualTo(sp - rule.Comp.TotemCost));

                // past the cooldown, but the clan has started on the only order
                rule.Comp.NextRerollAt = TimeSpan.Zero;
                entMan.GetComponent<BadBloodHuntGroundComponent>(fresh).Progress = 0.5f;
                Assert.That(orders.TryPetition(party.Leader), Is.False, "a ground the clan has started on is kept");
                Assert.That(rules.GetSp(), Is.EqualTo(sp - rule.Comp.TotemCost));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    private void AssertAt(EntityUid ground, MapCoordinates site, string message)
    {
        var at = Server.System<SharedTransformSystem>().GetMapCoordinates(ground);
        Assert.That(at.MapId, Is.EqualTo(site.MapId), message);
        Assert.That((at.Position - site.Position).Length(), Is.LessThan(0.01f), message);
    }

    // a near-band block east of the Lodge and a far-band block west of it, so the picker always finds ground
    private async Task LayGround(TestMapData map)
    {
        await Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
            for (var y = -12; y <= 12; y++)
            {
                for (var x = 26; x <= 44; x++)
                {
                    tiles.Add((map.Tile.GridIndices + new Vector2i(x, y), map.Tile.Tile));
                    tiles.Add((map.Tile.GridIndices + new Vector2i(-16 - x, y), map.Tile.Tile));
                }
            }

            Server.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
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
