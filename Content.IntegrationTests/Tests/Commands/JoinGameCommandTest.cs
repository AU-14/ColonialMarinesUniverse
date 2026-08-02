using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Commands;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Commands;

[TestFixture]
[TestOf(typeof(JoinGameCommand))]
public sealed class JoinGameCommandTest
{
    private const string TestMap = "JoinGameCommandTestMap";
    private const string SelectedJob = "JoinGameCommandSelectedJob";
    private const string FallbackJob = "JoinGameCommandFallbackJob";

    [TestPrototypes]
    private static readonly string Prototypes = $@"
- type: playTimeTracker
  id: PlayTimeJoinGameCommandSelectedJob

- type: playTimeTracker
  id: PlayTimeJoinGameCommandFallbackJob

- type: job
  id: {SelectedJob}
  playTimeTracker: PlayTimeJoinGameCommandSelectedJob

- type: job
  id: {FallbackJob}
  playTimeTracker: PlayTimeJoinGameCommandFallbackJob

- type: gameMap
  id: {TestMap}
  mapName: {TestMap}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
      - type: StationNameSetup
        mapNameTemplate: ""Join Game Command Test Station""
      - type: StationJobs
        availableJobs:
          {SelectedJob}: [1, 1]
          {FallbackJob}: [-1, -1]
";

    [Test]
    public async Task SelectedJobIsPreservedDuringDelayedRoundEnd()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });

        var server = pair.Server;
        var config = server.ResolveDependency<IConfigurationManager>();
        var console = server.ResolveDependency<IConsoleHost>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var jobs = entityManager.System<SharedJobSystem>();
        var minds = entityManager.System<MindSystem>();
        var stationJobs = entityManager.System<StationJobsSystem>();
        var stations = entityManager.System<StationSystem>();
        var ticker = entityManager.System<GameTicker>();

        config.SetCVar(CCVars.GameMap, TestMap);
        config.SetCVar(RMCCVars.RMCDelayRoundEnd, true);
        await pair.SetJobPriorities((FallbackJob, JobPriority.High));

        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        var station = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            station = stations.GetStations().Single(uid =>
                stationJobs.TryGetJobSlot(uid, SelectedJob, out _));

            ticker.EndRound();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
        });

        var session = server.PlayerMan.Sessions.Single();
        var stationId = entityManager.GetNetEntity(station).Id;
        await server.WaitPost(() =>
            console.GetSessionShell(session).ExecuteCommand($"joingame {SelectedJob} {stationId}"));
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.Not.Null);
            var mind = minds.GetMind(session.AttachedEntity!.Value);
            Assert.That(entityManager.EntityExists(mind));
            Assert.That(jobs.MindTryGetJobId(mind, out var actualJob));
            Assert.That(actualJob, Is.EqualTo(SelectedJob));
        });

        await pair.CleanReturnAsync();
    }
}
