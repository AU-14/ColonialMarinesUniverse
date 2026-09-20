using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Console;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class GovforShipRoundTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: gameMap
          id: CMUTestGovforColonyMap
          mapName: Test colony
          mapPath: /Maps/Test/empty.yml
          minPlayers: 0
          stations:
            Empty:
              stationProto: StandardNanotrasenStation
              components:
              - type: StationNameSetup
                mapNameTemplate: Test colony
        - type: entity
          id: CMUTestGovforColony
          components:
          - type: RMCPlanetMapPrototype
            mapId: CMUTestGovforColonyMap
            govforinship: true
            govfordropships: 0
            opfordropships: 0
        - type: gamePreset
          id: CMUTestGovforShipRound
          supportedPlanets: [CMUTestGovforColony]
          rules:
          - PlatoonSpawn
          - RemoveAllJobs
          - AddGovfor
        """;

    [TestCase("Almayer", "USCM")]
    [TestCase("Almayer", "WEYU")]
    [TestCase("USSBushRedux", "USCM")]
    [TestCase("USSBushRedux", "WEYU")]
    public async Task SelectedPlatoonVendorsAndLateJoinWorkAfterRoundStart(string shipId, string platoonId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var ticker = entities.System<GameTicker>();
        var stations = entities.System<StationSystem>();
        var stationJobs = entities.System<StationJobsSystem>();
        var zLevels = entities.System<CMUZLevelsSystem>();
        var platoon = server.ProtoMan.Index<PlatoonPrototype>(platoonId);
        // Disable the external voice service before connecting the client, which requests its catalog in the lobby.
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCCVars.TTSEnabled, false);
            server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, true);
            server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.RestartRound();
        });
        await pair.Connect();
        var session = server.PlayerMan.Sessions.Single();
        var prefs = server.ResolveDependency<IServerPreferencesManager>();
        const string jobId = "AU14JobGOVFORSquadRifleman";
        HumanoidCharacterProfile originalProfile = null!;

        await server.WaitPost(() =>
        {
            var profile = (HumanoidCharacterProfile) prefs.GetPreferences(session.UserId).Characters[0];
            originalProfile = profile;
            prefs.SetProfile(session.UserId, 0, profile.WithAllegiance(platoon.Allegiance)).GetAwaiter().GetResult();
            var round = entities.System<AuRoundSystem>();
            Assert.That(round.SetPlanet("CMUTestGovforColony"), Is.True);
            round.SetGovforShip(shipId);
            entities.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = platoon;
            ticker.SetGamePreset("CMUTestGovforShipRound");
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);

        var shipStation = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(session.AttachedEntity, Is.Null, "The player must still be in the lobby for a real late join.");
            var shipGrid = entities.EntityQueryEnumerator<ShipFactionComponent, TransformComponent>();
            EntityUid? shipMap = null;
            while (shipGrid.MoveNext(out var grid, out var faction, out var transform))
            {
                if (faction.Faction != "govfor")
                    continue;
                shipStation = stations.GetOwningStation(grid)!.Value;
                shipMap = transform.MapUid;
                break;
            }
            Assert.That(shipMap, Is.Not.Null);
            Assert.That(stationJobs.GetAvailableJobs(shipStation).Select(job => job.Id), Does.Contain(jobId));

            var expected = new List<(EntityCoordinates Coordinates, string Prototype)>();
            var vendorSet = server.ProtoMan.Index(platoon.VendorSet!.Value).Vendors;
            var markers = entities.AllEntityQueryEnumerator<VendorMarkerComponent, TransformComponent>();
            while (markers.MoveNext(out var marker, out var transform))
            {
                if (!marker.Ship || !zLevels.IsSameZNetwork(transform.MapUid, shipMap!.Value))
                    continue;
                if (!vendorSet.TryGetValue(marker.Class, out var vendor))
                    continue;
                Assert.That(marker.Spawned, Is.True, $"Unprocessed {marker.Class} marker");
                expected.Add((transform.Coordinates, vendor.Id));
            }
            Assert.That(expected.Count, Is.GreaterThan(20));
            var spawned = new List<(EntityCoordinates Coordinates, string Prototype)>();
            var query = entities.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var metadata, out var transform))
                spawned.Add((transform.Coordinates, metadata.EntityPrototype?.ID));
            foreach (var vendor in expected)
                Assert.That(spawned.Count(candidate => candidate == vendor), Is.EqualTo(1), $"{shipId}/{platoonId}: {vendor}");
        });

        await server.WaitPost(() => server.ResolveDependency<IConsoleHost>().GetSessionShell(session)
            .ExecuteCommand($"joingame {jobId} {entities.GetNetEntity(shipStation).Id}"));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.Not.Null);
            var player = session.AttachedEntity!.Value;
            var mind = entities.System<MindSystem>().GetMind(player);
            Assert.That(entities.System<SharedJobSystem>().MindTryGetJobId(mind, out var actualJob), Is.True);
            Assert.That(actualJob, Is.EqualTo(jobId));
            Assert.That(stations.GetOwningStation(player), Is.EqualTo(shipStation), "Late join must spawn aboard the selected Govfor ship.");
            Assert.That(ticker.PlayerGameStatuses[session.UserId], Is.EqualTo(PlayerGameStatus.JoinedGame));
        });
        await server.WaitPost(() => prefs.SetProfile(session.UserId, 0, originalProfile).GetAwaiter().GetResult());
        await pair.CleanReturnAsync();
    }
}
