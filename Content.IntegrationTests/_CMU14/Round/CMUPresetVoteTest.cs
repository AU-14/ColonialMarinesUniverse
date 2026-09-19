using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.Voting;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
[NonParallelizable]
public sealed class CMUPresetVoteTest : GameTest
{
    private const string PresetA = "CMUTestPresetVoteA";
    private const string PresetB = "CMUTestPresetVoteB";

    [TestPrototypes]
    private const string Prototypes = """
        - type: gamePreset
          id: CMUTestPresetVoteA
          name: ui-vote-gamemode-title
          description: ui-vote-gamemode-title
          showInVote: true

        - type: gamePreset
          id: CMUTestPresetVoteB
          name: ui-vote-gamemode-title
          description: ui-vote-gamemode-title
          showInVote: true
        """;

    public override PoolSettings PoolSettings => new()
    {
        InLobby = true,
        // Round history intentionally survives the pool's normal round restart cleanup.
        Fresh = true,
        Destructive = true,
    };

    [SetUp]
    public async Task ConfigureVotes()
    {
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.VoteTimerPreset, 0);
            Server.System<GameTicker>().PauseStart();
        });
    }

    [Test]
    public async Task BallotsDoNotReplaceLastPlayedPreset()
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            ticker.SetGamePreset(PresetA);
            ticker.StartRound();
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(PresetA));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            FinishVoteFor(PresetB);
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(PresetA));
            Assert.That(ticker.Preset?.ID, Is.EqualTo(PresetB));

            ticker.RestartRound();
            ticker.PauseStart();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

            // A second ballot in the next lobby must still exclude the round that ran.
            var vote = CreatePresetVote();
            Assert.That(vote.VotesPerOption.ContainsKey(PresetA), Is.False);
            Assert.That(vote.VotesPerOption.ContainsKey(PresetB), Is.True);
            vote.Cancel();
            Votes.Update();

            ticker.StartRound();
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(PresetB));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            vote = CreatePresetVote();
            Assert.That(vote.VotesPerOption.ContainsKey(PresetA), Is.True);
            Assert.That(vote.VotesPerOption.ContainsKey(PresetB), Is.False);
            vote.Cancel();
            Votes.Update();
        });
    }

    [Test]
    public async Task UnplayedLobbyWinnerRemainsEligibleAfterRestart()
    {
        await Server.WaitAssertion(() =>
        {
            FinishVoteFor(PresetA);
            Server.System<GameTicker>().RestartRound();
            Server.System<GameTicker>().PauseStart();

            var vote = CreatePresetVote();
            Assert.That(vote.VotesPerOption.ContainsKey(PresetA), Is.True);
            Assert.That(vote.VotesPerOption.ContainsKey(PresetB), Is.True);
            vote.Cancel();
            Votes.Update();
        });
    }

    [Test]
    public async Task SoleEligiblePresetIsRetained()
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            ticker.SetGamePreset(PresetA);
            ticker.StartRound();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var presets = new Dictionary<string, string> { [PresetA] = "mode" };
            Server.System<CMUPresetVoteSystem>().RemoveLastPlayedPreset(presets);
            Assert.That(presets.Keys, Is.EquivalentTo(new[] { PresetA }));
        });
    }

    private IVoteManager Votes => Server.ResolveDependency<IVoteManager>();

    private IVoteHandle CreatePresetVote()
    {
        Votes.CreateStandardVote(null, StandardVoteType.Preset);
        return Votes.ActiveVotes.Single();
    }

    private void FinishVoteFor(string preset)
    {
        var vote = CreatePresetVote();
        var options = vote.VotesPerOption.Keys.ToList();
        var index = options.IndexOf(preset);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        vote.CastVote(ServerSession!, index);
        Votes.Update();
        Assert.That(vote.Finished, Is.True);
        Assert.That(Server.System<GameTicker>().Preset?.ID, Is.EqualTo(preset));
    }
}
