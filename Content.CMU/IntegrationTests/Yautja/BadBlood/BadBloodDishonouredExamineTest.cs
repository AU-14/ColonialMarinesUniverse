using Content.Server.Examine;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-202: a Bad Blood reads as dishonoured, with no rank, trophy score or honour worth
[TestFixture]
public sealed class BadBloodDishonouredExamineTest
{
    private const string Hunter = "CMUMobYautja";
    private const string Marine = "CMMobHuman";

    // fragments of cmu-yautja-trophy-record-examine and cmu-yautja-honor-worth-examine
    private const string RankLine = "Yautja rank:";
    private const string ScoreLine = "Trophy score:";
    private const string WorthLine = "is worth";

    [Test]
    public async Task BadBloodExamineShowsDishonourInPlaceOfRankAndWorth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var examine = entMan.System<ExamineSystem>();
                var dishonoured = server.ResolveDependency<ILocalizationManager>().GetString("cmu-bad-blood-dishonoured-examine");

                EntityUid Spawn(string prototype, bool record)
                {
                    var uid = entMan.SpawnEntity(prototype, map.GridCoords);
                    spawned.Add(uid);
                    if (record)
                        entMan.EnsureComponent<YautjaTrophyRecordComponent>(uid);
                    return uid;
                }

                var hunter = Spawn(Hunter, true);
                var otherHunter = Spawn(Hunter, true);
                var badBlood = Spawn(BadBloodTestHelpers.GruntPrototype, true);
                var otherBadBlood = Spawn(BadBloodTestHelpers.LeaderPrototype, true);
                var marine = Spawn(Marine, false);

                Assert.That(entMan.HasComponent<YautjaBadBloodComponent>(badBlood), Is.True);
                Assert.That(entMan.HasComponent<YautjaBadBloodComponent>(otherBadBlood), Is.True);

                string Read(EntityUid target, EntityUid examiner)
                {
                    return examine.GetExamineText(target, examiner).ToString();
                }

                // a Bad Blood, seen by a hunter, by another Bad Blood and by themself
                foreach (var examiner in new[] { hunter, otherBadBlood, badBlood })
                {
                    var text = Read(badBlood, examiner);
                    Assert.Multiple(() =>
                    {
                        Assert.That(text, Does.Contain(dishonoured), $"examiner {examiner}");
                        Assert.That(text, Does.Not.Contain(RankLine));
                        Assert.That(text, Does.Not.Contain(ScoreLine));
                        Assert.That(text, Does.Not.Contain(WorthLine), "a Bad Blood has no honour worth");
                    });
                }

                // a Bad Blood examiner reads no honour worth on anyone
                Assert.Multiple(() =>
                {
                    Assert.That(Read(marine, badBlood), Does.Not.Contain(WorthLine));
                    Assert.That(Read(hunter, badBlood), Does.Not.Contain(WorthLine));
                    Assert.That(Read(marine, marine), Does.Not.Contain(dishonoured));
                });

                // honourable hunters read exactly what they did before
                var seenHunter = Read(hunter, otherHunter);
                Assert.Multiple(() =>
                {
                    Assert.That(seenHunter, Does.Contain(RankLine));
                    Assert.That(seenHunter, Does.Contain(ScoreLine));
                    Assert.That(seenHunter, Does.Contain(WorthLine));
                    Assert.That(seenHunter, Does.Not.Contain(dishonoured));
                    Assert.That(Read(marine, hunter), Does.Contain(WorthLine));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (server.EntMan.EntityExists(uid))
                        server.EntMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }
}
