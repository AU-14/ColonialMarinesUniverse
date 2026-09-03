using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.Threats;
using Content.Shared.Shuttles.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class RmcErtThirdPartyDropshipMapTest
{
    private static readonly (ResPath Path, int Leaders, int Members, int Entities)[] DropshipMaps =
    {
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_clf_shuttle.yml"), 4, 8, 3),
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_cmb_shuttle.yml"), 4, 8, 3),
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_pmc_shuttle.yml"), 1, 10, 3),
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_response_shuttle.yml"), 4, 8, 3),
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_spp_shuttle.yml"), 4, 8, 3),
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_tse_shuttle.yml"), 4, 8, 3),
        (new("/Maps/CMU14/ShuttlesDropships/rmc_ert_tsepa_shuttle.yml"), 4, 8, 3),
    };

    private static readonly (ResPath Path, int Leaders, int Members, int Entities)[] MapFormatDropshipMaps =
    {
        (new("/Maps/CMU14/ShuttlesDropships/genericthirdpartyshuttle.yml"), 4, 5, 3),
        (new("/Maps/CMU14/Shuttles/black_ert.yml"), 4, 8, 3),
        (new("/Maps/CMU14/Shuttles/cmbtransport_ert.yml"), 4, 6, 3),
        (new("/Maps/CMU14/Shuttles/icrctransport_ert.yml"), 4, 6, 3),
        (new("/Maps/CMU14/Shuttles/white_ert.yml"), 4, 8, 3),
    };

    private static readonly ProtoId<ThirdPartyPrototype> MissionariesParty = "MissionariesParty";

    [Test]
    public async Task RmcErtThirdPartyDropshipsLoadWithSpawnPlans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapLoader = server.System<MapLoaderSystem>();
            var mapSystem = server.System<SharedMapSystem>();

            foreach (var (path, expectedLeaders, expectedMembers, expectedEntities) in DropshipMaps)
            {
                mapSystem.CreateMap(out var mapId);
                Assert.That(mapLoader.TryLoadGrid(mapId, path, out var grid), Is.True, path.ToString());
                var gridUid = grid!.Value.Owner;

                AssertThirdPartyMarkerCounts(
                    entities,
                    new[] { gridUid },
                    path,
                    expectedLeaders,
                    expectedMembers,
                    expectedEntities);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MapFormatThirdPartyDropshipsLoadWithSpawnPlans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapLoader = server.System<MapLoaderSystem>();
            var options = DeserializationOptions.Default with { InitializeMaps = true };

            foreach (var (path, expectedLeaders, expectedMembers, expectedEntities) in MapFormatDropshipMaps)
            {
                Assert.That(mapLoader.TryLoadMap(path, out _, out var grids, options), Is.True, path.ToString());
                Assert.That(grids, Is.Not.Empty, path.ToString());
                var gridUids = grids.Select(grid => grid.Owner).ToArray();

                AssertThirdPartyMarkerCounts(
                    entities,
                    gridUids,
                    path,
                    expectedLeaders,
                    expectedMembers,
                    expectedEntities);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RmcAlamoThreatLeaderMarkerLoads()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapLoader = server.System<MapLoaderSystem>();
            var mapSystem = server.System<SharedMapSystem>();

            mapSystem.CreateMap(out var mapId);
            Assert.That(
                mapLoader.TryLoadGrid(mapId, new ResPath("/Maps/_RMC14/alamo.yml"), out var grid),
                Is.True);
            var gridUids = new[] { grid!.Value.Owner };

            var leaderMarkers = 0;
            var markerQuery = entities.EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out _, out var marker, out var transform))
            {
                if (IsOnAnyGrid(transform, gridUids) &&
                    marker.ThreatMarkerType == ThreatMarkerType.Leader &&
                    !marker.ThirdParty)
                    leaderMarkers++;
            }

            Assert.That(leaderMarkers, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThirdPartyShuttleSpawnWaitsForManualLaunch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid genericDestination = EntityUid.Invalid;
        EntityUid returnedDestination = EntityUid.Invalid;
        EntityUid thirdPartyDestination = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var thirdPartySystem = server.System<ThirdPartySystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(MissionariesParty);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);

            genericDestination = entities.SpawnEntity("CMDropshipDestination", map.GridCoords);

            returnedDestination = entities.SpawnEntity("CMDropshipDestinationThirdPartyReturn", map.GridCoords);
            var returnDestination = entities.EnsureComponent<ThirdPartyDropshipReturnDestinationComponent>(
                returnedDestination);
            returnDestination.Shuttle = EntityUid.Invalid;

            thirdPartyDestination = entities.SpawnEntity("CMDropshipDestinationThirdPartyWhitelist", map.GridCoords);

            thirdPartySystem.SpawnThirdParty(thirdParty, partySpawn, false);
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            EntityUid? spawnedDropship = null;
            EntityUid? spawnedReturnDestination = null;
            var dropshipQuery = entities.EntityQueryEnumerator<DropshipComponent>();
            while (dropshipQuery.MoveNext(out var uid, out var dropship))
            {
                if (dropship.Destination is not { } destination ||
                    !entities.TryGetComponent(destination,
                        out ThirdPartyDropshipReturnDestinationComponent returnDestination) ||
                    returnDestination.Shuttle != uid)
                {
                    continue;
                }

                spawnedDropship = uid;
                spawnedReturnDestination = destination;
                break;
            }

            Assert.That(spawnedDropship, Is.Not.Null);
            Assert.That(spawnedReturnDestination, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(genericDestination).Ship,
                    Is.Null,
                    "Generic dropship destinations must not be selected for strict third-party shuttles.");
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(returnedDestination).Ship,
                    Is.Null,
                    "Returned third-party holding vectors must not be reused as active landing destinations.");
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(thirdPartyDestination).Ship,
                    Is.Null,
                    "The spawned third-party shuttle must not auto-launch to the active third-party landing destination.");
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(spawnedReturnDestination.Value).Ship,
                    Is.EqualTo(spawnedDropship.Value),
                    "The spawned third-party shuttle should stay parked at its generated deep-space return destination.");
                Assert.That(
                    entities.HasComponent<FTLComponent>(spawnedDropship.Value),
                    Is.False,
                    "The spawned third-party shuttle should wait for a manual launch instead of entering FTL.");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertThirdPartyMarkerCounts(
        IEntityManager entities,
        IReadOnlyCollection<EntityUid> gridUids,
        ResPath path,
        int expectedLeaders,
        int expectedMembers,
        int expectedEntities)
    {
        var leaderMarkers = 0;
        var memberMarkers = 0;
        var entityMarkers = 0;
        var navigationComputers = 0;
        var thirdPartyNavigationComputers = 0;

        var markerQuery = entities.EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out _, out var marker, out var transform))
        {
            if (!IsOnAnyGrid(transform, gridUids) ||
                !marker.ThirdParty)
            {
                continue;
            }

            if (marker.ThreatMarkerType == ThreatMarkerType.Leader)
                leaderMarkers++;
            if (marker.ThreatMarkerType == ThreatMarkerType.Member)
                memberMarkers++;
            if (marker.ThreatMarkerType == ThreatMarkerType.Entity)
                entityMarkers++;
        }

        var navigationQuery = entities.EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (navigationQuery.MoveNext(out var uid, out _, out var transform))
        {
            if (IsOnAnyGrid(transform, gridUids))
            {
                navigationComputers++;

                if (entities.TryGetComponent(uid, out WhitelistedShuttleComponent shuttle) &&
                    shuttle.Faction == "thirdparty" &&
                    shuttle.ShuttleType == DropshipDestinationComponent.DestinationType.Dropship &&
                    shuttle.AutoReturn)
                {
                    thirdPartyNavigationComputers++;
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(leaderMarkers, Is.EqualTo(expectedLeaders), path.ToString());
            Assert.That(memberMarkers, Is.EqualTo(expectedMembers), path.ToString());
            Assert.That(entityMarkers, Is.EqualTo(expectedEntities), path.ToString());
            Assert.That(navigationComputers, Is.GreaterThanOrEqualTo(1), path.ToString());
            Assert.That(thirdPartyNavigationComputers, Is.GreaterThanOrEqualTo(1), path.ToString());
        });
    }

    private static bool IsOnAnyGrid(TransformComponent transform, IReadOnlyCollection<EntityUid> gridUids)
    {
        return (transform.GridUid.HasValue && gridUids.Contains(transform.GridUid.Value)) ||
               gridUids.Contains(transform.ParentUid);
    }
}
