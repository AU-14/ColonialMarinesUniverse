using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
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
            Assert.That(zLevels.TryMapUp(map, out var upper), Is.True);
            var upperCoords = new EntityCoordinates(upper!.Value.Owner, new Vector2(2.5f, 66.5f));
            entities.SpawnEntity("VMarkerShipOverwatchConsole", upperCoords);

            var expected = new List<(EntityCoordinates Coordinates, string Prototype)>();
            var vendors = prototypes.Index(platoon.VendorSet!.Value).Vendors;
            var markers = entities.AllEntityQueryEnumerator<VendorMarkerComponent, TransformComponent>();
            while (markers.MoveNext(out var marker, out var transform))
            {
                if (!marker.Ship || !zLevels.IsSameZNetwork(transform.MapUid, map))
                    continue;

                if (vendors.TryGetValue(marker.Class, out var vendor))
                    expected.Add((transform.Coordinates, vendor.Id));
                else if (marker.Class == PlatoonMarkerClass.OverwatchConsole)
                    expected.Add((transform.Coordinates, faction == "govfor"
                        ? "RMCOverwatchConsoleGovforRotating" : "RMCOverwatchConsoleOpforRotating"));
                else if (marker.Class == PlatoonMarkerClass.RosterConsole)
                    expected.Add((transform.Coordinates, faction == "govfor"
                        ? "CMUGovforRosterConsole" : "CMUOpforRosterConsole"));
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
            });
        });

        await pair.CleanReturnAsync();
    }
}
