using System.Reflection;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Station.Systems;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.CMU14;
using Content.Shared.GameTicking;
using Moq;
using Robust.Shared.Player;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Round;

public sealed partial class GovforShipRoundTest
{
    [TestPrototypes]
    private const string VotingPrototypes = """
        - type: entity
          parent: CMUTestGovforColony
          id: CMUTestShipVoteColony
          components:
          - type: RMCPlanetMapPrototype
            platoonsGovfor: [USCM]
        - type: gamePreset
          id: CMUTestShipVoteRound
          supportedPlanets: [CMUTestShipVoteColony]
          requiresGovforVote: true
          requiresOpforVote: false
          rules:
          - PlatoonSpawn
          - RemoveAllJobs
          - AddGovfor
        """;

    [TestCase("USSBushRedux", "USS George W. Bush", "UNS Almayer")]
    [TestCase("Almayer", "UNS Almayer", "USS George W. Bush")]
    public async Task ShipVoteMajorityLoadsOnlyTheWinningShip(string winner, string stationName, string loserName)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var ticker = server.System<GameTicker>();
        var round = server.System<AuRoundSystem>();
        var votes = server.ResolveDependency<IVoteManager>();
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, true);
            server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            server.CfgMan.SetCVar(CCVars.VoteCarryoverEnabled, false);
            ticker.RestartRound();
        });
        // Only voter identities are mocked; tallying, callbacks and map loading are real.
        // Engine dummy sessions cannot receive the vote manager's network messages.
        var voters = Enumerable.Range(0, 3).Select(_ => new Mock<ICommonSession>().Object).ToArray();
        await server.WaitAssertion(() =>
        {
            Assert.That(round.SetPlanet("CMUTestShipVoteColony"), Is.True);
            round.SetPreset(server.ProtoMan.Index<GamePresetPrototype>("CMUTestShipVoteRound"));
            // Enter the real platoon -> ship vote stage after preset/planet selection.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var sequence = (int) typeof(AuRoundSystem).GetProperty("_voteSequenceId", flags)!.GetValue(round)!;
            typeof(AuRoundSystem).GetMethod("StartPlatoonVotes", flags)!.Invoke(round, new object[] { sequence });
            var platoon = votes.ActiveVotes.Single(vote => vote.Title == "Govfor Vote");
            foreach (var voter in voters)
                platoon.CastVote(voter, 0);
            FinishVote(votes, platoon);
        });
        await server.WaitRunTicks(30);
        await server.WaitAssertion(() =>
        {
            var ship = votes.ActiveVotes.Single(vote => vote.Title == "Govfor Ship Vote");
            var options = ship.VotesPerOption.Keys.Cast<string>().ToArray();
            Assert.That(options, Is.EquivalentTo(new[] { "USSBushRedux", "Almayer" }));
            var winningOption = Array.IndexOf(options, winner);
            ship.CastVote(voters[0], winningOption);
            ship.CastVote(voters[1], winningOption);
            ship.CastVote(voters[2], 1 - winningOption);
            Assert.That(ship.VotesPerOption[winner], Is.EqualTo(2));
            Assert.That(ship.CastVotes, Has.Count.EqualTo(3));
            FinishVote(votes, ship);
            Assert.That(round.GetSelectedGovforShip(), Is.EqualTo(winner));
            ticker.SetGamePreset("CMUTestShipVoteRound");
            ticker.StartRound();
        });
        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var stations = server.System<StationSystem>();
            var owners = new HashSet<EntityUid>();
            var query = entities.EntityQueryEnumerator<ShipFactionComponent>();
            while (query.MoveNext(out var grid, out var faction))
                if (faction.Faction == "govfor" && stations.GetOwningStation(grid) is { } station)
                    owners.Add(station);
            var names = owners.Select(uid => entities.GetComponent<MetaDataComponent>(uid).EntityName).ToArray();
            Assert.That(names, Does.Contain(stationName), "The voted map must actually be loaded.");
            Assert.That(names, Does.Not.Contain(loserName), "The losing ship must not be loaded as well.");
        });
        await pair.CleanReturnAsync();
    }

    private static void FinishVote(IVoteManager manager, IVoteHandle handle)
    {
        // Use the same tally/callback path as the timer, without sleeping through the vote duration.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var registration = handle.GetType().GetField("_reg", flags)!.GetValue(handle);
        manager.GetType().GetMethod("EndVote", flags)!.Invoke(manager, new[] { registration });
        Assert.That(handle.Finished, Is.True);
    }
}
