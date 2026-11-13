using System.Linq;
using Content.Client.Guidebook.Components;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;
using ClientCharacterInfoSystem = Content.Client.CharacterInfo.CharacterInfoSystem;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-106: a Bad Blood is not shown a clan rank, is not handed the honour code, and has an entry message of its
// own. An honourable hunter keeps all three
[TestFixture]
public sealed class BadBloodOnboardingTest : GameTest
{
    private const string Hunter = "CMUMobYautja";
    private const string HunterBracer = "CMUYautjaBracer";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";
    private const string HonourCode = "AU14YautjaHonorCode";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task BadBloodHasNoClanRankTitleAndAHunterStillDoes()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        EntityUid badBlood = default;
        EntityUid hunter = default;
        var ranks = new HashSet<string>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                foreach (var rank in YautjaRankMetadata.Order)
                {
                    ranks.Add(Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName));
                }

                badBlood = SEntMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
                hunter = SEntMan.SpawnEntity(Hunter, map.GridCoords.Offset(new Vector2(1, 0)));
                Assert.That(SEntMan.HasComponent<YautjaBadBloodComponent>(badBlood), Is.True);
                Assert.That(SEntMan.HasComponent<YautjaBadBloodComponent>(hunter), Is.False);
            });

            var badBloodTitle = await RequestJobTitle(badBlood);
            Assert.That(badBloodTitle, Is.Not.Null, "the server did not answer the character info request");
            Assert.That(ranks, Does.Not.Contain(badBloodTitle), "a Bad Blood must not be given a clan rank title");

            var hunterTitle = await RequestJobTitle(hunter);
            Assert.That(hunterTitle, Is.Not.Null, "the server did not answer the character info request");
            Assert.That(ranks, Does.Contain(hunterTitle), "an honourable hunter keeps the clan rank title");
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, null);
                foreach (var uid in new[] { badBlood, hunter })
                {
                    if (uid.IsValid() && SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }

    [Test]
    public async Task BadBloodBracerDropsTheHonourCodeAndTheEntryMessageExists()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(YautjaStatsSystem.BadBloodEntryLines, Is.Not.Empty);
            foreach (var line in YautjaStatsSystem.BadBloodEntryLines)
            {
                Assert.That(Loc.TryGetString(line, out var text), Is.True, $"missing locale string {line}");
                Assert.That(text, Is.Not.Empty);
            }
        });

        // the guide verb is a client component
        await Client.WaitAssertion(() =>
        {
            var factory = CEntMan.ComponentFactory;
            Assert.That(CProtoMan.Index<EntityPrototype>(HunterBracer).TryComp<GuideHelpComponent>(out var hunterGuide, factory),
                Is.True);
            Assert.That(hunterGuide!.Guides.Any(guide => guide.Id == HonourCode), Is.True,
                "the hunters' bracer keeps the honour code guide");

            Assert.That(CProtoMan.Index<EntityPrototype>(BadBloodBracer).TryComp<GuideHelpComponent>(out var badBloodGuide, factory),
                Is.True);
            Assert.That(badBloodGuide!.Guides.Any(guide => guide.Id == HonourCode), Is.False,
                "the Bad Blood bracer must not offer the honour code");
            Assert.That(badBloodGuide.Guides.Any(guide => guide.Id == "CMUGuideBadBlood"), Is.True,
                "the Bad Blood bracer opens the Bad Blood Clan guide");
        });
    }

    // puts the player in the body and asks for its character info the way the character window does
    private async Task<string?> RequestJobTitle(EntityUid body)
    {
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, body));
        await Pair.RunTicksSync(10);

        string? title = null;
        void OnUpdate(ClientCharacterInfoSystem.CharacterData data)
        {
            title = data.JobTitle;
        }

        var info = Client.System<ClientCharacterInfoSystem>();
        await Client.WaitPost(() =>
        {
            info.OnCharacterUpdate += OnUpdate;
            info.RequestCharacterInfo();
        });

        await Pair.RunTicksSync(10);
        await Client.WaitPost(() => info.OnCharacterUpdate -= OnUpdate);
        return title;
    }
}
