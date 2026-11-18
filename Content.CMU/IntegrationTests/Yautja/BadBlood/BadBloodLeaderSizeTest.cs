using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.Sprite;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-611: the clan's Leader is drawn larger than the grunts, and the size follows leadership
[TestFixture]
public sealed class BadBloodLeaderSizeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TheLeaderStandsTallerAndTheSizeFollowsLeadership()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var scale = Server.System<SharedScaleVisualsSystem>();
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetLeader(), Is.EqualTo(party.Leader),
                    "Test setup: the rule picked the landing Leader");
                Assert.Multiple(() =>
                {
                    Assert.That(scale.GetSpriteScale(party.Leader), Is.EqualTo(new Vector2(1.1f)),
                        "the Leader is drawn larger than the clan");
                    Assert.That(scale.GetSpriteScale(grunt), Is.EqualTo(Vector2.One));
                    Assert.That(entMan.HasComponent<ScaleVisualsComponent>(grunt), Is.False);
                });

                // leadership moves: so does the size
                Assert.That(Server.System<BadBloodCampRuleSystem>().SetLeader(grunt), Is.True);
            });

            await Server.WaitRunTicks(2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var scale = Server.System<SharedScaleVisualsSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(scale.GetSpriteScale(grunt), Is.EqualTo(new Vector2(1.1f)), "the new Leader grows");
                    Assert.That(entMan.HasComponent<ScaleVisualsComponent>(party.Leader), Is.False,
                        "the old Leader returns to normal size");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }
}
