using System.Collections;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.EUI;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using ClientPromptEui = Content.Client.CMU14.Yautja.BadBlood.BadBloodAscensionPromptEui;
using ClientVoteEui = Content.Client.CMU14.Yautja.BadBlood.BadBloodAscensionVoteEui;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-113 through the player's hands: the other ascension tests call TryStandAsCandidate directly and never touch
// the Stone or the windows. Here a connected player is offered the Lodge, claims it at the Stone and votes.
[TestFixture]
public sealed class AscensionStoneClaimTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Stone = "CMUBadBloodCampAscensionStone";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task PlayerIsOfferedTheLodgeClaimsItAndCanReopenTheVote()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);
        var spawned = new List<EntityUid>();
        var grunt = party.Grunts[0];
        var rival = party.Grunts[1];
        var stone = EntityUid.Invalid;
        BaseEui? firstPrompt = null;

        try
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession!, grunt);
                spawned.Add(SEntMan.SpawnEntity(Lodge, map.GridCoords));
                stone = SEntMan.SpawnEntity(Stone, SEntMan.GetComponent<TransformComponent>(grunt).Coordinates);
                spawned.Add(stone);
            });

            await Pair.RunTicksSync(5);

            await Server.WaitPost(() =>
                Server.System<MobStateSystem>().ChangeMobState(party.Leader, MobState.Dead));

            await Pair.RunTicksSync(5);
            await Pair.RunUntilSynced();

            // FLOW-02: with a Lodge standing, the offer still goes to every eligible Bad Blood at once
            await Server.WaitAssertion(() =>
            {
                var ascension = Server.System<BadBloodAscensionSystem>();
                Assert.That(ascension.IsVoteOpen(), Is.True, "the vote opens when the Leader dies");
                Assert.That(ascension.GetVote()!.FromBracer, Is.False);
                firstPrompt = ServerPrompt();
                Assert.That(firstPrompt, Is.Not.Null, "the Lodge vote offers the claim without a trip to the Stone");
            });

            await Client.WaitAssertion(() =>
                Assert.That(ClientHas<ClientPromptEui>(), Is.True, "the offer reaches the client"));

            // FLOW-03: claiming at the Stone while the offer is open replaces it rather than doing nothing
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedInteractionSystem>().AltInteract(grunt, stone), Is.True,
                    "alt-click finds the claim verb on the Stone");
                var prompt = ServerPrompt();
                Assert.That(prompt, Is.Not.Null);
                Assert.That(prompt, Is.Not.SameAs(firstPrompt), "the Stone opens a fresh offer");
                Assert.That(firstPrompt!.IsShutDown, Is.True, "the old offer is closed");
            });

            // a rival stands first: the player gets the vote window, closes it, and opens it again from the Stone
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodAscensionSystem>().TryStandAsCandidate(rival), Is.True);
                var vote = ServerVoteUi();
                Assert.That(vote, Is.Not.Null, "a candidacy opens the vote window");
                vote!.Close();
                Assert.That(ServerVoteUi(), Is.Null);

                // FLOW-04: the Stone's view verb brings the window back
                var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(stone, grunt, typeof(AlternativeVerb));
                var view = verbs.FirstOrDefault(v => v.Text == Loc.GetString("cmu-bad-blood-ascension-view-verb"));
                Assert.That(view, Is.Not.Null, "the Stone offers to show the running vote");
                Server.System<SharedVerbSystem>().ExecuteVerb(view!, grunt, stone);
                Assert.That(ServerVoteUi(), Is.Not.Null, "the vote window is open again");
            });

            await Server.WaitAssertion(() =>
                ServerPrompt()!.HandleMessage(new BadBloodAscensionPromptMessage(true)));

            await Pair.RunTicksSync(5);
            await Pair.RunUntilSynced();

            await Server.WaitAssertion(() =>
            {
                var vote = Server.System<BadBloodAscensionSystem>().GetVote();
                Assert.That(vote, Is.Not.Null, "the vote is still running");
                Assert.That(vote!.Candidates, Does.Contain(grunt), "accepting the offer makes the grunt a candidate");
            });

            await Client.WaitAssertion(() =>
                Assert.That(ClientHas<ClientVoteEui>(), Is.True, "the vote window reaches the client"));
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private BaseEui? ServerPrompt() => FirstOf("_promptUis");

    private BaseEui? ServerVoteUi() => FirstOf("_voteUis");

    private BaseEui? FirstOf(string field)
    {
        var uis = (IDictionary) typeof(BadBloodAscensionSystem)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Server.System<BadBloodAscensionSystem>())!;

        return uis.Values.Cast<BaseEui>().FirstOrDefault(eui => !eui.IsShutDown);
    }

    private bool ClientHas<T>()
    {
        var manager = Client.ResolveDependency<Content.Client.Eui.EuiManager>();
        var open = (IDictionary) typeof(Content.Client.Eui.EuiManager)
            .GetField("_openUis", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!;

        return open.Values.Cast<object>().Any(entry => entry.GetType().GetField("Eui")!.GetValue(entry) is T);
    }
}
