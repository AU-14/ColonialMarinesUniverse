using System.Linq;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.CMU14.VendorMarker;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.util;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkLandingTest
{
    [Test]
    public async Task RotationLandingZonesAndHangarsAcceptMohawks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var failures = new List<string>();
        var ships = new Dictionary<string, EntityUid>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var variant in new[] { "omaha", "midway" })
            {
                entities.System<SharedMapSystem>().CreateMap(out var mapId);
                Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                    new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
                ships.Add(variant, loaded!.Value.Owner);
            }
        });

        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        // Use the same pools as map voting, including presets that share a rotation.
        var planets = prototypes.EnumeratePrototypes<GamePresetPrototype>()
            .SelectMany(preset => GamePlanetPoolPrototype.ExpandPlanetIds(prototypes, preset.PlanetPool, preset.SupportedPlanets))
            .Distinct().ToArray();
        var carriers = prototypes.EnumeratePrototypes<PlatoonPrototype>()
            .SelectMany(platoon => platoon.PossibleShips).ToHashSet();
        var mapIds = planets.Select(planet => ((RMCPlanetMapPrototypeComponent) prototypes.Index<EntityPrototype>(planet)
                .Components["RMCPlanetMapPrototype"].Component).MapId)
            .Concat(carriers)
            .Distinct().Order().ToArray();
        Assert.That(mapIds, Is.Not.Empty);
        foreach (var mapId in mapIds)
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var prototype = prototypes.Index<GameMapPrototype>(mapId);
                var loader = entities.System<MapLoaderSystem>();
                var options = DeserializationOptions.Default with { InitializeMaps = true, PauseMaps = true };
                Assert.That(loader.TryLoadMap(prototype.MapPath, out var ground, out _, options), Is.True);
                var groundUid = ground!.Value.Owner;
                var levels = new Dictionary<EntityUid, int> { [groundUid] = 0 };
                for (var index = 0; index < prototype.MapsAbove.Count; index++)
                {
                    Assert.That(loader.TryLoadMap(prototype.MapsAbove[index], out var upper, out _, options), Is.True);
                    levels.Add(upper!.Value.Owner, index + 1);
                }
                for (var index = 0; index < prototype.MapsBelow.Count; index++)
                {
                    Assert.That(loader.TryLoadMap(prototype.MapsBelow[index], out var lower, out _, options), Is.True);
                    levels.Add(lower!.Value.Owner, -index - 1);
                }
                var zLevels = entities.System<CMUZLevelsSystem>();
                var network = zLevels.CreateZNetwork();
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, levels), Is.True);
                var transform = entities.System<SharedTransformSystem>();
                // Carrier hangar destinations are created from vendor markers during round setup.
                foreach (var marker in entities.EntityQuery<VendorMarkerComponent>(true).ToArray())
                {
                    var markerTransform = entities.GetComponent<TransformComponent>(marker.Owner);
                    if (!carriers.Contains(mapId) || !marker.Ship ||
                        markerTransform.MapUid is not { } markerMap || !levels.ContainsKey(markerMap) ||
                        marker.Class != PlatoonMarkerClass.DropshipDestination)
                        continue;
                    entities.SpawnAttachedTo("CMDropshipDestinationHome", markerTransform.Coordinates,
                        rotation: markerTransform.LocalRotation);
                }
                // Third-party destinations are not selectable by platoon dropships.
                var destinations = entities.EntityQuery<DropshipDestinationComponent>(true)
                    .Where(d => entities.GetComponent<TransformComponent>(d.Owner).MapUid is { } map && levels.ContainsKey(map) &&
                                d.Destinationtype == DropshipDestinationComponent.DestinationType.Dropship &&
                                d.FactionController != "thirdparty").ToArray();
                Assert.That(destinations, Is.Not.Empty, $"{mapId} must exercise its LZs or generated hangar destinations.");
                TestContext.Progress.WriteLine($"{mapId}: {destinations.Length} landing pads on {levels.Count} levels");
                foreach (var destination in destinations)
                foreach (var (variant, ship) in ships)
                {
                    var xform = entities.GetComponent<TransformComponent>(destination.Owner);
                    var blocked = new HashSet<Vector2i>();
                    var coordinates = entities.System<MultiDeckDropshipSystem>().GetLandingOrigin(ship, xform.Coordinates, destination.Owner);
                    if (!entities.System<MultiDeckDropshipSystem>().IsLandingClear(ship, coordinates,
                            transform.GetWorldRotation(destination.Owner), blocked))
                    {
                        var failure = $"{prototype.ID} ({variant}): {entities.GetComponent<MetaDataComponent>(destination.Owner).EntityName} at {xform.LocalPosition} z{levels[xform.MapUid!.Value]}: {blocked.Count} blocked tiles ({string.Join(", ", blocked)})";
                        failures.Add(failure);
                        TestContext.Progress.WriteLine(failure);
                    }
                }
                foreach (var map in levels.Keys)
                    entities.DeleteEntity(map);
                entities.DeleteEntity(network);
            });
            await pair.RunTicksSync(1);
        }
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var ship in ships.Values)
                pair.Server.EntMan.DeleteEntity(ship);
        });
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
        await pair.CleanReturnAsync();
    }
}
