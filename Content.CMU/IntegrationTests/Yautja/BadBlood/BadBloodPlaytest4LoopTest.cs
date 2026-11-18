using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// TEST-603: the Playtest 4 loop end to end, in the order a round meets it. Each piece has its own focused test; this
// one proves they hold together: a clan pitches before the drop, marines learn nothing until they land, the landing
// draws the Lodge region once, and the kennel stocks hounds the moment the Lodge stands. The Totem landing gate has
// its own test (ClanTotemTest.MarinesLearnOfTotemsOnlyOnceTheyLand)
[TestFixture]
public sealed class BadBloodPlaytest4LoopTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Kennel = "CMUBadBloodCampKennel";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task PitchBeforeTheDropThenLandRevealsTheRegionAndTheKennelIsFree()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3, startLanded: false);
        var spawned = new List<EntityUid>();
        var regionBefore = 0;
        EntityUid kennel = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var camp = Server.System<BadBloodCampSystem>();
                regionBefore = camp.LodgeRegionAnnounced;
                spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords));
                kennel = Server.EntMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));
                spawned.Add(kennel);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var stock = Server.EntMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.HaveMarinesLanded(), Is.False, "Test setup: the marines are still in orbit");
                    Assert.That(camp.LodgeRegionAnnounced, Is.EqualTo(regionBefore),
                        "marines in orbit are not told where the Lodge is");
                    Assert.That(stock.Current, Is.GreaterThanOrEqualTo(stock.MinStock),
                        "the kennel offers its stock at pitch");
                });

                Assert.That(rules.MarkMarinesLanded(), Is.True);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(Server.System<BadBloodCampSystem>().LodgeRegionAnnounced, Is.EqualTo(regionBefore + 1),
                        "the landing draws the Lodge region once");
                    Assert.That(Server.System<BadBloodCampSystem>().IsLodgeRevealed(), Is.False, "the region is not the exact Lodge blip");
                    Assert.That(rules.MarkMarinesLanded(), Is.False, "the landing latches once");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }
}
