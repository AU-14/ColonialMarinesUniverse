using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Round;
using Content.Shared.AU14.Scenario;
using Content.Shared._CMU14.Threats;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._AU14.ThirdParty;

[TestFixture]
public sealed class RmcErtThirdPartyDropshipMapTest
{
    private static readonly (ResPath Path, int Leaders, int Members, int Entities)[] DropshipMaps =
    {
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_clf_shuttle.yml"), 4, 8, 3),
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_cmb_shuttle.yml"), 4, 8, 3),
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_pmc_shuttle.yml"), 4, 8, 3),
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_response_shuttle.yml"), 4, 8, 3),
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_spp_shuttle.yml"), 4, 8, 3),
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_tse_shuttle.yml"), 4, 8, 3),
        (new("/Maps/_AU14/ShuttlesDropships/rmc_ert_tsepa_shuttle.yml"), 4, 8, 3),
    };

    private static readonly (ResPath Path, int Leaders, int Members, int Entities)[] MapFormatDropshipMaps =
    {
        (new("/Maps/_AU14/ShuttlesDropships/genericthirdpartyshuttle.yml"), 4, 5, 3),
        (new("/Maps/_CMU14/Shuttles/black_ert.yml"), 4, 8, 3),
        (new("/Maps/_CMU14/Shuttles/cmbtransport_ert.yml"), 4, 6, 3),
        (new("/Maps/_CMU14/Shuttles/icrctransport_ert.yml"), 4, 6, 3),
        (new("/Maps/_CMU14/Shuttles/white_ert.yml"), 4, 8, 3),
    };

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

                AssertStandaloneThirdPartyMarkerCounts(
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

                AssertStandaloneThirdPartyMarkerCounts(
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
    public async Task RmcAlamoThreatLeaderMarkerLoadsAsScenarioCompatibilityMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapLoader = server.System<MapLoaderSystem>();
            var options = DeserializationOptions.Default with { InitializeMaps = true };

            Assert.That(
                mapLoader.TryLoadMap(new ResPath("/Maps/_RMC14/alamo.yml"), out _, out var grids, options),
                Is.True);
            Assert.That(grids, Is.Not.Empty);
            var gridUids = grids.Select(grid => grid.Owner).ToArray();

            var scenarioLeaderMarkers = 0;
            var legacyLeaderMarkers = 0;
            var markerQuery = entities.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out var uid, out var marker, out var transform))
            {
                if (!IsOnAnyGrid(transform, gridUids) ||
                    marker.Kind != SpawnMarkerKind.ThreatMarker ||
                    !marker.Tags.Contains(ScenarioMarkerTags.ForceHostile) ||
                    !marker.Tags.Contains(ScenarioMarkerTags.Bucket(ThreatMarkerType.Leader.ToString())) ||
                    !marker.Tags.Contains(ScenarioMarkerTags.MarkerId(string.Empty)))
                {
                    continue;
                }

                scenarioLeaderMarkers++;
                if (entities.TryGetComponent(uid, out ThreatSpawnMarkerComponent legacyMarker) &&
                    legacyMarker.ThreatMarkerType == ThreatMarkerType.Leader &&
                    !legacyMarker.ThirdParty)
                {
                    legacyLeaderMarkers++;
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(scenarioLeaderMarkers, Is.EqualTo(1));
                Assert.That(legacyLeaderMarkers, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertStandaloneThirdPartyMarkerCounts(
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
        var legacyThirdPartyMarkers = 0;
        var cooldownMarkers = 0;
        var navigationComputers = 0;
        var thirdPartyNavigationComputers = 0;

        var markerQuery = entities.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var uid, out var marker, out var transform))
        {
            if (!IsOnAnyGrid(transform, gridUids) ||
                marker.Kind != SpawnMarkerKind.ThirdPartyMarker ||
                !marker.Tags.Contains(ScenarioMarkerTags.ForceThirdParty))
            {
                continue;
            }

            if (marker.Tags.Contains(ScenarioMarkerTags.Bucket(ThreatMarkerType.Leader.ToString())))
                leaderMarkers++;
            if (marker.Tags.Contains(ScenarioMarkerTags.Bucket(ThreatMarkerType.Member.ToString())))
                memberMarkers++;
            if (marker.Tags.Contains(ScenarioMarkerTags.Bucket(ThreatMarkerType.Entity.ToString())))
                entityMarkers++;

            if (entities.HasComponent<ThreatSpawnMarkerComponent>(uid))
                legacyThirdPartyMarkers++;
            if (entities.HasComponent<ScenarioSpawnMarkerCooldownComponent>(uid))
                cooldownMarkers++;
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
            Assert.That(legacyThirdPartyMarkers, Is.Zero, path.ToString());
            Assert.That(cooldownMarkers, Is.EqualTo(expectedLeaders + expectedMembers + expectedEntities), path.ToString());
        });
    }

    private static bool IsOnAnyGrid(TransformComponent transform, IReadOnlyCollection<EntityUid> gridUids)
    {
        return (transform.GridUid.HasValue && gridUids.Contains(transform.GridUid.Value)) ||
               gridUids.Contains(transform.ParentUid);
    }
}
