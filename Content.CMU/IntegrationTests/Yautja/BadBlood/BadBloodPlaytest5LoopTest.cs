#pragma warning disable RA0002 // shortens the order's seize time and reads rule state for the test
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// TEST-701: the Playtest 5 loop end to end. Each piece has its own focused test; this one proves they hold together
// in round order: a Lodge stands, the clan is ordered to a Hunt Ground, a clanmate holds it and a Totem rises in the
// clan's count; a Beast-Master takes up the caste and the clan's hounds answer to them; when the Beast-Master takes
// the lead they shed the caste, and the hounds fall back to them as the Hunt Leader.
[TestFixture]
public sealed class BadBloodPlaytest5LoopTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Hound = "CMUMobYautjaBadBloodHound";
    private static readonly ProtoId<BadBloodCastePrototype> BeastMaster = "BeastMaster";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task AnOrderBecomesATotemAndTheHoundsFollowTheBeastMaster()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();
        var (holder, beastMaster) = (party.Grunts[0], party.Grunts[1]);
        var totemsBefore = 0;
        var seizedBefore = 0;
        EntityUid hound = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var xform = Server.System<SharedTransformSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // the order clock is FLOW-703's own test; here the order is placed by hand where the holder stands
                rule.Comp.HuntOrdersEnabled = false;
                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords.Offset(new Vector2(-30, 0))));

                totemsBefore = rules.GetTotems().Count;
                seizedBefore = orders.OrdersSeizedCount;
                var ground = orders.IssueOrderAt(xform.GetMapCoordinates(holder));
                Assert.That(ground, Is.Not.Null, "Test setup: the order is issued");
                entMan.GetComponent<BadBloodHuntGroundComponent>(ground!.Value).SeizeTime = TimeSpan.FromSeconds(5);
                Assert.That(orders.CountActive() + rules.GetTotems().Count, Is.LessThanOrEqualTo(rules.GetTotemLimit()),
                    "orders and Totems together stay within the limit");

                Assert.That(Server.System<BadBloodCasteSystem>().TryTakeCaste(beastMaster, BeastMaster), Is.True);
                hound = entMan.SpawnEntity(Hound, map.GridCoords.Offset(new Vector2(0, 2)));
                spawned.Add(hound);
            });

            await Pair.RunSeconds(8);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var orders = Server.System<BadBloodHuntOrderSystem>();
                var beasts = Server.System<BadBloodBeastSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(orders.OrdersSeizedCount, Is.EqualTo(seizedBefore + 1), "the clan seized the ground");
                    Assert.That(rules.GetTotems().Count, Is.EqualTo(totemsBefore + 1), "a Totem rose in the clan's count");
                    Assert.That(beasts.GetMasterFor(hound), Is.EqualTo(beastMaster), "the hound answers to the Beast-Master");
                });

                spawned.AddRange(rules.GetTotems());

                Assert.That(rules.SetLeader(beastMaster), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.EntMan.HasComponent<BadBloodBeastMasterComponent>(beastMaster), Is.False,
                        "the new Hunt Leader sheds the Beast-Master caste");
                    Assert.That(beasts.GetMasterFor(hound), Is.EqualTo(beastMaster),
                        "with no Beast-Master the hound answers to the Hunt Leader");
                });
            });
        }
        finally
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
}
