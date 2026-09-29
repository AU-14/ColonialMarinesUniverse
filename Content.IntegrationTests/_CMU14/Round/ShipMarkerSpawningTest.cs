using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Hospital;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.EntitySerialization;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class ShipMarkerSpawningTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestShipMarkerPlanet
          components:
          - type: RMCPlanetMapPrototype
            mapId: USSBushRedux
            inRotation: false
            govforinship: true
            opforinship: true
            govfordropships: 0
            opfordropships: 0
        """;

    [TestCase("WEYU", "govfor")]
    [TestCase("USCM", "opfor")]
    public async Task ShipMarkersSpawnVendorsAndConsolesAcrossDecks(string platoonId, string faction)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var round = entities.System<AuRoundSystem>();
            var platoons = entities.System<PlatoonSpawnRuleSystem>();
            var zLevels = entities.System<CMUZLevelsSystem>();
            var prototypes = server.ProtoMan;
            var platoon = prototypes.Index<PlatoonPrototype>(platoonId);
            if (faction == "govfor")
                platoons.SelectedGovforPlatoon = platoon;
            else
                platoons.SelectedOpforPlatoon = platoon;

            Assert.That(round.SetPlanet("CMUTestShipMarkerPlanet"), Is.True);
            var grids = ticker.LoadGameMap(prototypes.Index<GameMapPrototype>("USSBushRedux"),
                out var mapId, DeserializationOptions.Default with { InitializeMaps = true });
            Assert.That(grids, Is.Not.Empty);
            foreach (var grid in grids)
                entities.EnsureComponent<ShipFactionComponent>(grid).Faction = faction;

            var map = entities.System<SharedMapSystem>().GetMap(mapId);

            var expected = new List<(EntityCoordinates Coordinates, string Prototype)>();
            var vendors = prototypes.Index(platoon.VendorSet!.Value).Vendors;
            var markers = entities.AllEntityQueryEnumerator<VendorMarkerComponent, TransformComponent>();
            while (markers.MoveNext(out var marker, out var transform))
            {
                if (!marker.Ship || !zLevels.IsSameZNetwork(transform.MapUid, map))
                    continue;

                if (vendors.TryGetValue(marker.Class, out var vendor))
                    expected.Add((transform.Coordinates, vendor.Id));
            }

            Assert.That(expected.Count, Is.GreaterThan(20));
            Assert.That(ticker.StartGameRule("PlatoonSpawn"), Is.True);

            var spawned = new List<(EntityCoordinates Coordinates, string Prototype)>();
            var query = entities.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var metadata, out var transform))
                spawned.Add((transform.Coordinates, metadata.EntityPrototype?.ID));

            Assert.Multiple(() =>
            {
                foreach (var item in expected)
                    Assert.That(spawned.Count(candidate => candidate == item), Is.EqualTo(1),
                        $"{faction}/{platoonId}: expected one {item.Prototype} at {item.Coordinates}");

                // Govfor machines baked into the map must become their Opfor variants on an Opfor ship
                Assert.That(spawned.Count(c => c.Prototype == "AU14WithdrawConsoleGovFor"),
                    Is.EqualTo(faction == "govfor" ? 1 : 0));
                Assert.That(spawned.Count(c => c.Prototype == "AU14WithdrawConsoleOpFor"),
                    Is.EqualTo(faction == "opfor" ? 1 : 0));
                Assert.That(spawned.Count(c => c.Prototype == "CMAirlockGovforGlassLocked"),
                    faction == "govfor" ? Is.GreaterThan(5) : Is.EqualTo(0));
                Assert.That(spawned.Count(c => c.Prototype == "CMAirlockOpforGlassLocked"),
                    faction == "opfor" ? Is.GreaterThan(5) : Is.EqualTo(0));
                Assert.That(spawned.Count(c => c.Prototype == "CMUResearchDataTerminalGovfor"),
                    Is.EqualTo(faction == "govfor" ? 2 : 0));
                Assert.That(spawned.Count(c => c.Prototype == "CMUResearchDataTerminalOpfor"),
                    Is.EqualTo(faction == "opfor" ? 2 : 0));
                Assert.That(spawned.Count(c => c.Prototype == "CMUHospitalEmergencyComputerGovfor"),
                    Is.EqualTo(faction == "govfor" ? 1 : 0));
                Assert.That(spawned.Count(c => c.Prototype == "CMUHospitalEmergencyComputerOpfor"),
                    Is.EqualTo(faction == "opfor" ? 1 : 0));

                var hospitals = entities.AllEntityQueryEnumerator<HospitalEmergencyComputerComponent, TransformComponent>();
                while (hospitals.MoveNext(out var hospital, out var transform))
                {
                    if (!zLevels.IsSameZNetwork(transform.MapUid, map))
                        continue;
                    Assert.That(hospital.LandingZone, Is.Not.Null, "Ship hospital must find the landing zone on its other deck.");
                    Assert.That(zLevels.IsSameZNetwork(entities.GetComponent<TransformComponent>(hospital.LandingZone!.Value).MapUid, map),
                        Is.True, "Ship hospital must keep its own ship's landing zone.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IdenticalShipsResolveTerminalsByOwnerAndColonyMarkersKeepColonyAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var govShip = await pair.CreateTestMap();
        var opShip = await pair.CreateTestMap();
        var colony = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var round = entities.System<AuRoundSystem>();
            Assert.That(round.SetPlanet("CMUTestShipMarkerPlanet"), Is.True);
            foreach (var (ship, faction) in new[] { (govShip, "govfor"), (opShip, "opfor") })
            {
                entities.EnsureComponent<ShipFactionComponent>(ship.GridCoords.EntityId).Faction = faction;
                var marker = entities.SpawnEntity("CMUVMarkerShipResearchTerminal", ship.GridCoords);
                // Ship ownership must override baked faction flags and avoid a second ground spawn.
                entities.GetComponent<VendorMarkerComponent>(marker).Govfor = true;
                entities.SpawnEntity("CMUVMarkerShipHospitalEmergencyComputer", ship.GridCoords);
            }
            entities.SpawnEntity("CMUVMarkerColonyHospitalEmergencyComputer", colony.GridCoords);
            Assert.That(ticker.StartGameRule("PlatoonSpawn"), Is.True);

            var terminals = entities.AllEntityQueryEnumerator<ResearchDataTerminalComponent, TransformComponent>();
            var terminalCount = 0;
            while (terminals.MoveNext(out var terminal, out var transform))
            {
                if (transform.GridUid != govShip.GridCoords.EntityId && transform.GridUid != opShip.GridCoords.EntityId)
                    continue;
                terminalCount++;
                Assert.That(terminal.Faction,
                    Is.EqualTo(transform.GridUid == govShip.GridCoords.EntityId ? "govfor" : "opfor"));
            }
            Assert.That(terminalCount, Is.EqualTo(2));

            var computers = entities.AllEntityQueryEnumerator<HospitalEmergencyComputerComponent, TransformComponent>();
            var computerCount = 0;
            var access = entities.System<AccessReaderSystem>();
            var credentials = new[] { "AU14AccessGovforMedical", "AU14AccessGovforCommand",
                "AU14AccessOpforMedical", "AU14AccessOpforCommand", "CMAccessColonyMedbay", "CMAccessColonyCommand",
                "AU14AccessColonyHeadPhysician" };
            while (computers.MoveNext(out var uid, out _, out var transform))
            {
                var grid = transform.GridUid;
                if (grid != govShip.GridCoords.EntityId && grid != opShip.GridCoords.EntityId && grid != colony.GridCoords.EntityId)
                    continue;
                computerCount++;
                var faction = grid == govShip.GridCoords.EntityId ? "Govfor" : grid == opShip.GridCoords.EntityId ? "Opfor" : "Colony";
                Assert.That(entities.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID,
                    Is.EqualTo("CMUHospitalEmergencyComputer" + faction));
                var reader = entities.GetComponent<AccessReaderComponent>(uid);
                foreach (var credential in credentials)
                    Assert.That(access.IsAllowed(new List<ProtoId<AccessLevelPrototype>> { credential }, [], uid, reader),
                        Is.EqualTo(credential.Contains(faction)), $"{faction} computer / {credential}");
                Assert.That(access.IsAllowed([], [], uid, reader), Is.False);
            }
            Assert.That(computerCount, Is.EqualTo(3));
        });
        await pair.CleanReturnAsync();
    }
}
