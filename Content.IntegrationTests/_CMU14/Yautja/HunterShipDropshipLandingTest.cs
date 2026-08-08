using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Maps;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class HunterShipDropshipLandingTest
{
    private static readonly string[] HunterDestinationPrototypes =
    [
        "CMUHunterShipYautjaLandingPadAFTLBeacon",
        "CMUHunterShipYautjaLandingPadBFTLBeacon",
        "CMUHunterShipYautjaHangarA",
    ];

    [Test]
    public async Task HunterShuttlesArriveAtTheirSelectedHunterShipDestinations()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Destructive = true,
        });
        var server = pair.Server;
        var departures = await Task.WhenAll(
            pair.CreateTestMap(),
            pair.CreateTestMap(),
            pair.CreateTestMap());

        EntityUid hunterShip = default;
        var destinations = new Dictionary<string, EntityUid>();
        var shuttles = new List<(EntityUid Shuttle, EntityUid Console, EntityUid Destination)>();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();

            Assert.That(loader.TryLoadMap(
                new ResPath("/Maps/_CMU14/huntership_upper.yml"),
                out var hunterMap,
                out var hunterGrids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(hunterMap, Is.Not.Null);
            Assert.That(hunterGrids, Has.Count.EqualTo(1));
            hunterShip = hunterMap!.Value.Owner;

            var hunterGrid = hunterGrids!.Single().Owner;
            var destinationPrototypes = HunterDestinationPrototypes.ToHashSet();
            var entities = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (entities.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (transform.GridUid == hunterGrid &&
                    metadata.EntityPrototype?.ID is { } prototype &&
                    destinationPrototypes.Contains(prototype))
                {
                    destinations[prototype] = uid;
                }
            }

            Assert.That(destinations.Keys, Is.EquivalentTo(HunterDestinationPrototypes));

            foreach (var (departure, prototype) in departures.Zip(HunterDestinationPrototypes))
            {
                Assert.That(loader.TryLoadGrid(
                    departure.MapId,
                    new ResPath("/Maps/_CMU14/Shuttles/hunter_shuttle.yml"),
                    out var shuttleGrid), Is.True);
                Assert.That(shuttleGrid, Is.Not.Null);

                var shuttle = shuttleGrid!.Value.Owner;
                var console = FindNavigationConsole(entMan, shuttle);
                shuttles.Add((shuttle, console, destinations[prototype]));
            }
        });

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var dropship = entMan.System<DropshipSystem>();
            var docking = entMan.System<DockingSystem>();

            foreach (var (shuttle, console, destination) in shuttles)
            {
                var computer = (console, entMan.GetComponent<DropshipNavigationComputerComponent>(console));
                Assert.That(dropship.FlyTo(computer, destination, null, startupTime: 0f, hyperspaceTime: 0f), Is.True);
                Assert.That(entMan.HasComponent<FTLComponent>(shuttle), Is.True);

                var ftl = entMan.GetComponent<FTLComponent>(shuttle);
                Assert.That(docking.GetDockingConfigAt(shuttle, ftl.TargetCoordinates.EntityId, ftl.TargetCoordinates, ftl.TargetAngle), Is.Null);
            }
        });

        await PoolManager.WaitUntil(server, () =>
            shuttles.All(shuttle =>
                server.EntMan.TryGetComponent<FTLComponent>(shuttle.Shuttle, out var ftl) &&
                ftl.State == FTLState.Cooldown),
            maxTicks: 60);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            foreach (var (shuttle, _, destination) in shuttles)
            {
                Assert.That(entMan.GetComponent<TransformComponent>(shuttle).MapUid, Is.EqualTo(hunterShip));

                var distance = Vector2.Distance(
                    transform.GetMapCoordinates(shuttle).Position,
                    transform.GetMapCoordinates(destination).Position);
                Assert.That(distance, Is.LessThanOrEqualTo(2f), $"{entMan.ToPrettyString(shuttle)} must arrive at its selected destination, not near the Hunter Ship grid.");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid FindNavigationConsole(IEntityManager entMan, EntityUid shuttle)
    {
        var consoles = entMan.EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (consoles.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.GridUid == shuttle)
                return uid;
        }

        Assert.Fail($"Hunter shuttle {entMan.ToPrettyString(shuttle)} has no navigation console.");
        return default;
    }
}
