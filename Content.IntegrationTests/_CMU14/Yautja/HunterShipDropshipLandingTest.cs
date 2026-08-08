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
using Content.Shared.CCVar;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
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
        var departures = new[]
        {
            await pair.CreateTestMap(),
            await pair.CreateTestMap(),
            await pair.CreateTestMap(),
        };

        EntityUid hunterShip = default;
        EntityUid hunterGrid = default;
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

            hunterGrid = hunterGrids!.Single().Owner;
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
        Assert.That(shuttles, Has.Count.EqualTo(HunterDestinationPrototypes.Length),
            "The landing regression must exercise all three Hunter Ship destinations.");

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var dropship = entMan.System<DropshipSystem>();
            var docking = entMan.System<DockingSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            server.CfgMan.SetCVar(CCVars.FTLTravelTime, 0.1f);
            server.CfgMan.SetCVar(CCVars.FTLArrivalTime, 0.1f);
            server.CfgMan.SetCVar(CCVars.FTLCooldown, 0.1f);

            foreach (var (shuttle, console, destination) in shuttles)
            {
                var computer = (console, entMan.GetComponent<DropshipNavigationComputerComponent>(console));
                var destinationTransform = entMan.GetComponent<TransformComponent>(destination);
                var destinationComponent = entMan.GetComponent<DropshipDestinationComponent>(destination);
                Assert.That(destinationComponent.FactionController, Is.EqualTo("yautja"));
                Assert.That(destinationTransform.GridUid, Is.Not.Null);
                Assert.That(entMan.HasComponent<MapGridComponent>(destinationTransform.GridUid.Value), Is.True);
                Assert.That(dropship.FlyTo(computer, destination, null, startupTime: 0f, hyperspaceTime: 0f), Is.True);
                Assert.That(entMan.HasComponent<FTLComponent>(shuttle), Is.True);

                var ftl = entMan.GetComponent<FTLComponent>(shuttle);
                Assert.That(ftl.TargetCoordinates.EntityId, Is.EqualTo(hunterGrid),
                    $"{entMan.ToPrettyString(shuttle)} must keep the selected Hunter Ship destination grid-relative during FTL.");
                Assert.That(docking.GetDockingConfigAt(shuttle, ftl.TargetCoordinates.EntityId, ftl.TargetCoordinates, ftl.TargetAngle), Is.Null);
            }

            var hunterTransform = entMan.GetComponent<TransformComponent>(hunterGrid);
            transform.SetLocalPositionRotation(
                hunterGrid,
                hunterTransform.LocalPosition + new Vector2(4f, -3f),
                hunterTransform.LocalRotation + Angle.FromDegrees(90),
                hunterTransform);

            var hunterBody = entMan.GetComponent<PhysicsComponent>(hunterGrid);
            var hunterFixtures = entMan.GetComponent<FixturesComponent>(hunterGrid);
            var physics = entMan.System<PhysicsSystem>();
            physics.SetLinearVelocity(hunterGrid, Vector2.Zero, body: hunterBody);
            physics.SetAngularVelocity(hunterGrid, 0f, body: hunterBody);
            physics.SetBodyType(hunterGrid, BodyType.Static, manager: hunterFixtures, body: hunterBody);
            physics.SetFixedRotation(hunterGrid, true, manager: hunterFixtures, body: hunterBody);
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
                var shuttleTransform = entMan.GetComponent<TransformComponent>(shuttle);
                Assert.That(shuttleTransform.MapUid, Is.EqualTo(hunterShip));
                Assert.That(shuttleTransform.ParentUid, Is.EqualTo(hunterShip),
                    $"{entMan.ToPrettyString(shuttle)} must be parented to the Hunter Ship map, not nested under its grid.");

                var shuttleGrid = entMan.GetComponent<MapGridComponent>(shuttle);
                var shuttleAabb = transform.GetWorldMatrix(shuttle).TransformBox(shuttleGrid.LocalAABB);
                var destinationPosition = transform.GetMapCoordinates(destination).Position;
                var centerDistance = Vector2.Distance(shuttleAabb.Center, destinationPosition);
                Assert.That(centerDistance, Is.EqualTo(0f).Within(0.01f),
                    $"{entMan.ToPrettyString(shuttle)} hull center must match the landing pad's current position.");
                Assert.That(transform.GetWorldRotation(shuttle).GetCardinalDir(),
                    Is.EqualTo(transform.GetWorldRotation(destination).GetCardinalDir()),
                    $"{entMan.ToPrettyString(shuttle)} must match the landing pad's current orientation.");
                var shuttlePhysics = entMan.GetComponent<PhysicsComponent>(shuttle);
                Assert.That(shuttlePhysics.BodyType, Is.EqualTo(BodyType.Static),
                    $"{entMan.ToPrettyString(shuttle)} must remain static after landing on the Hunter Ship.");
                Assert.That(shuttlePhysics.FixedRotation, Is.True,
                    $"{entMan.ToPrettyString(shuttle)} must preserve its initial fixed rotation after landing.");
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
