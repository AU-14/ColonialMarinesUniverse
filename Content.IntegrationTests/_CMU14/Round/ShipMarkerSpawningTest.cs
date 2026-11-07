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

    private static readonly Robust.Shared.Prototypes.EntProtoId PlatoonSpawnPrototype = "PlatoonSpawn";

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
            Assert.That(ticker.StartGameRule(PlatoonSpawnPrototype), Is.True);

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
