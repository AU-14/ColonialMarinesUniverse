using System.Linq;
using Content.Client.Players.PlayTimeTracking;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Lobby;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using ClientAccess = Content.Client.CMU14.Lobby.CMUEventAccessSystem;

namespace Content.IntegrationTests.CMU14.Lobby;

[TestFixture]
public sealed class CMUEventLobbyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    private const string GovforJob = "CMUEventTestGovfor";
    private const string OpforJob = "CMUEventTestOpfor";
    private const string OfficerJob = "CMUEventTestOfficer";
    private static readonly ProtoId<GameMapPrototype> StationMap = "CMUEventTestStation";

    [TestPrototypes]
    private const string Prototypes = """
- type: playTimeTracker
  id: CMUEventTestPlaytime

- type: job
  id: CMUEventTestGovfor
  name: cmu-event-code-title
  playTimeTracker: CMUEventTestPlaytime
  roundSide: Govfor
  roundRole: CMUEventTestRifleman

- type: job
  id: CMUEventTestOpfor
  name: cmu-event-code-title
  playTimeTracker: CMUEventTestPlaytime
  roundSide: Opfor
  roundRole: CMUEventTestRifleman

- type: job
  id: CMUEventTestOfficer
  name: cmu-event-code-title
  playTimeTracker: CMUEventTestPlaytime
  roundSide: Opfor
  roundRole: CMUEventTestCommander

- type: gameMap
  id: CMUEventTestStation
  minPlayers: 0
  mapName: Event test
  mapPath: /Maps/Test/empty.yml
  stations:
    Empty:
      mapNameTemplate: Event test
      stationProto: StandardNanotrasenStation
      components:
      - type: StationJobs
        availableJobs:
          CMUEventTestGovfor: [2, 2]
          CMUEventTestOpfor: [2, 2]
          CMUEventTestOfficer: [1, 1]
""";

    private EntityUid CreateStation()
    {
        var map = SProtoMan.Index(StationMap);
        return SEntMan.System<StationSystem>().InitializeNewStation(map.Stations["Empty"], null, "Event test", map);
    }

    [Test]
    public async Task CodesGateAssignmentDirectSpawnsAndClientRequirements()
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        HumanoidCharacterProfile profile = default!;

        void AssertAssigned(string expected)
        {
            var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile> { [ServerSession!.UserId] = profile };
            var assignments = SEntMan.System<StationJobsSystem>().AssignJobs(profiles, new[] { station });
            Assert.That(assignments[ServerSession.UserId].Item1?.Id, Is.EqualTo(expected));
        }

        await Server.WaitAssertion(() =>
        {
            profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
                {
                    [GovforJob] = JobPriority.Low,
                    [OpforJob] = JobPriority.Medium,
                    [OfficerJob] = JobPriority.High,
                });
            station = CreateStation();
            SEntMan.System<StationSystem>().AddGridToStation(station, map.Grid);
            var spawnPoint = SEntMan.SpawnEntity(null, map.GridCoords);
            var spawn = SEntMan.AddComponent<SpawnPointComponent>(spawnPoint);
            spawn.Job = OfficerJob;
            spawn.SpawnType = SpawnPointType.Job;
            SEntMan.System<GameTicker>().SetGamePreset("Insurgency");
            var console = Server.ResolveDependency<IConsoleHost>();
            console.ExecuteCommand("eventaccess enable");
            console.ExecuteCommand("eventaccess set opfor side-code opfor CMUEventTestRifleman CMUEventTestCommander");
            console.ExecuteCommand("eventaccess set command command-code opfor CMUEventTestCommander");
            AssertAssigned(GovforJob);
            Assert.That(SEntMan.System<GameTicker>().DoSpawn(ServerSession!, profile, station, OfficerJob, true,
                out _, out _, out _), Is.False, "A direct join must not bypass an invitation or leave the lobby.");
            Assert.That(SEntMan.System<GameTicker>().PlayerGameStatuses[ServerSession!.UserId],
                Is.Not.EqualTo(PlayerGameStatus.JoinedGame));
        });

        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            var requirements = Client.ResolveDependency<JobRequirementsManager>();
            Assert.That(requirements.IsAllowed(CProtoMan.Index<JobPrototype>(OpforJob), profile, out _), Is.False);
            CEntMan.System<ClientAccess>().OpenCodeWindow();
            CEntMan.System<ClientAccess>().RequestCode("side-code");
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() => AssertAssigned(OpforJob));
        await Client.WaitAssertion(() =>
        {
            var requirements = Client.ResolveDependency<JobRequirementsManager>();
            Assert.That(requirements.IsAllowed(CProtoMan.Index<JobPrototype>(OpforJob), profile, out _), Is.True);
            Assert.That(requirements.IsAllowed(CProtoMan.Index<JobPrototype>(OfficerJob), profile, out _), Is.False,
                "The side code does not unlock a separately protected command role.");
        });

        await Task.Delay(2100); // The invitation cooldown uses real time, including while the lobby is paused.
        await Client.WaitPost(() => Client.ResolveDependency<IConsoleHost>().ExecuteCommand("eventcode command-code"));
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() => AssertAssigned(OfficerJob));
        await Client.WaitAssertion(() => Assert.That(
            Client.ResolveDependency<JobRequirementsManager>()
                .IsAllowed(CProtoMan.Index<JobPrototype>(OfficerJob), profile, out _), Is.True));

        await Server.WaitAssertion(() =>
        {
            var access = SEntMan.System<CMUEventAccessSystem>();
            access.SetGroup("command", "replacement-code", new() { OfficerJob });
            AssertAssigned(OpforJob);
            access.RemoveGroup("command");
            AssertAssigned(OfficerJob);
            access.SetGroup("command", "replacement-code", new() { OfficerJob });
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() => Assert.That(CEntMan.System<ClientAccess>().IsLocked(OfficerJob), Is.True,
            "Code rotation must revoke the client's earlier unlock as well as server assignment eligibility."));
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<CMUEventAccessSystem>().RemoveGroup("command");
            Assert.That(SEntMan.System<GameTicker>().DoSpawn(ServerSession!, profile, station, OfficerJob, true,
                out _, out _, out _), Is.True,
                "The same station and spawn point must allow the job once its invitation gate is open.");
        });
    }

    [Test]
    public async Task LobbyAndLiveSlotChangesRespectRoundLifecycle()
    {
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<CMUEventSlotsSystem>();
            var access = SEntMan.System<CMUEventAccessSystem>();
            access.SetEnabled(true);
            access.SetGroup("opfor", "old-code", new() { OpforJob });
            Assert.That(access.TryRedeem(ServerSession!, "old-code", out var message), Is.True, message);
            slots.TryChange("opfor", OpforJob, 9, false, out _);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(access.TryRedeem(ServerSession!, "old-code", out _), Is.False,
                "The next round cannot accept old codes.");

            Server.ResolveDependency<IConsoleHost>().ExecuteCommand("eventaccess enable");
            Server.ResolveDependency<IConsoleHost>().ExecuteCommand("eventslots add opfor CMUEventTestRifleman 3");
            Assert.That(slots.TryChange("opfor", OfficerJob, 0, true, out message), Is.True, message);
            Assert.That(slots.TryChange("govfor", GovforJob, -1, true, out message), Is.True, message);
            Server.CfgMan.SetCVar(CCVars.GameMap, StationMap.Id);
            SEntMan.System<GameTicker>().SetGamePreset((GamePresetPrototype) null!);
        });

        // Exercise the actual lobby -> map setup -> event slots -> job assignment ordering.
        await Server.WaitPost(() => SEntMan.System<GameTicker>().StartRound());
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var jobs = SEntMan.System<StationJobsSystem>();
            var slots = SEntMan.System<CMUEventSlotsSystem>();
            var station = SEntMan.System<StationSystem>().GetStations()
                .Single(uid => jobs.TryGetJobSlot(uid, OpforJob, out _));

            Assert.That(jobs.GetJobs(station)[OpforJob], Is.EqualTo(5),
                "Only this round's queued addition applies, after the map's two normal slots exist.");
            Assert.That(jobs.GetRoundStartJobs(station)[OpforJob], Is.EqualTo(5), "Ready players must see the additional slots.");
            Assert.That(jobs.GetJobs(station)[OfficerJob], Is.Zero);
            Assert.That(jobs.GetJobs(station)[GovforJob], Is.Null);
            Assert.That(jobs.GetOverflowJobs(station), Does.Contain(new ProtoId<JobPrototype>(GovforJob)));

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new CMURoundJobsReadyEvent());
            Assert.That(jobs.GetJobs(station)[OpforJob], Is.EqualTo(5), "A queued change may only apply once.");

            Assert.That(slots.TryChange("opfor", OpforJob, -1, false, out var message), Is.True, message);
            Assert.That(jobs.GetJobs(station)[OpforJob], Is.EqualTo(4), "Live commands must take effect immediately.");
            Assert.That(slots.TryChange("opfor", OpforJob, -100, false, out _), Is.False);
            Assert.That(jobs.GetJobs(station)[OpforJob], Is.EqualTo(4), "A rejected removal must leave capacity intact.");
            Assert.That(slots.TryChange("govfor", GovforJob, 1, true, out message), Is.True, message);
            Assert.That(jobs.GetOverflowJobs(station), Does.Not.Contain(new ProtoId<JobPrototype>(GovforJob)),
                "Making a role finite must remove the old overflow path.");
        });
    }

    [Test]
    public async Task AdminPanelToggleSuspendsGatesAndRejectsChangesAfterDeadminning()
    {
        CMUEventSetupEui panel = default!;
        EntityUid station = default;
        await Server.WaitAssertion(() =>
        {
            station = CreateStation();
            var access = SEntMan.System<CMUEventAccessSystem>();
            access.SetGroup("opfor", "test-code", new() { OpforJob });
            Assert.That(access.CanAccessJob(ServerSession!.UserId, OpforJob), Is.True,
                "Configured codes must not restrict ordinary rounds until event mode is enabled.");
            Assert.That(SEntMan.System<CMUEventSlotsSystem>().TryChange("opfor", OpforJob, 3, false, out _), Is.False);
            panel = new CMUEventSetupEui();
            Server.ResolveDependency<EuiManager>().OpenEui(panel, ServerSession!);
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            panel.HandleMessage(new CMUEventSetupMessage(CMUEventSetupAction.Enable));
            panel.HandleMessage(new CMUEventSetupMessage(CMUEventSetupAction.SaveGroup,
                group: "command", code: "officer-code", side: "opfor", roles: [OfficerJob]));
            panel.HandleMessage(new CMUEventSetupMessage(CMUEventSetupAction.AddSlots,
                side: "opfor", roles: [OpforJob], amount: 3));
            Assert.That(SEntMan.System<CMUEventAccessSystem>().CanAccessJob(ServerSession!.UserId, OfficerJob), Is.False);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.System<ClientAccess>().Enabled, Is.True);
            Assert.That(CEntMan.System<ClientAccess>().IsLocked(OfficerJob), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            panel.HandleMessage(new CMUEventSetupMessage(CMUEventSetupAction.Disable));
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new CMURoundJobsReadyEvent());
            Assert.That(SEntMan.System<StationJobsSystem>().GetJobs(station)[OpforJob], Is.EqualTo(2),
                "Disabling event mode before setup finishes must stop its queued slot changes.");
            Assert.That(SEntMan.System<CMUEventAccessSystem>().CanAccessJob(ServerSession!.UserId, OfficerJob), Is.True);

            Server.ResolveDependency<IAdminManager>().DeAdmin(ServerSession!);
            panel.HandleMessage(new CMUEventSetupMessage(CMUEventSetupAction.Enable));
            panel.HandleMessage(new CMUEventSetupMessage(CMUEventSetupAction.RemoveGroup, group: "command"));
            Assert.That(SEntMan.System<CMUEventAccessSystem>().Enabled, Is.False);
            Assert.That(SEntMan.System<CMUEventAccessSystem>().GroupNames, Does.Contain("command"),
                "A stale admin window must not mutate settings after permission loss.");
            Assert.That(((CMUEventSetupState) panel.GetNewState()).Groups, Is.Empty);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.System<ClientAccess>().Enabled, Is.False);
            Assert.That(CEntMan.System<ClientAccess>().IsLocked(OfficerJob), Is.False);
        });
    }
}
