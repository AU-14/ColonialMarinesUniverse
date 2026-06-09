using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Threats;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._AU14.ThirdParty;

[TestFixture]
public sealed class RmcErtThirdPartyDropshipMapTest
{
    private static readonly ResPath[] DropshipMaps =
    {
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_clf_shuttle.yml"),
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_cmb_shuttle.yml"),
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_pmc_shuttle.yml"),
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_response_shuttle.yml"),
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_spp_shuttle.yml"),
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_tse_shuttle.yml"),
        new("/Maps/_AU14/ShuttlesDropships/rmc_ert_tsepa_shuttle.yml"),
    };

    [Test]
    public async Task RmcErtThirdPartyDropshipsLoadWithSpawnContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapLoader = server.System<MapLoaderSystem>();

            foreach (var path in DropshipMaps)
            {
                Assert.That(mapLoader.TryLoadMap(path, out _, out var grids), Is.True, path.ToString());
                Assert.That(grids, Is.Not.Empty, path.ToString());

                var leaderMarkers = 0;
                var memberMarkers = 0;
                var entityMarkers = 0;
                var navigationComputers = 0;

                var markerQuery = entities.EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
                while (markerQuery.MoveNext(out _, out var marker, out var transform))
                {
                    if (!transform.GridUid.HasValue ||
                        !grids.Contains(transform.GridUid.Value) ||
                        !marker.ThirdParty)
                    {
                        continue;
                    }

                    switch (marker.ThreatMarkerType)
                    {
                        case ThreatMarkerType.Leader:
                            leaderMarkers++;
                            break;
                        case ThreatMarkerType.Member:
                            memberMarkers++;
                            break;
                        case ThreatMarkerType.Entity:
                            entityMarkers++;
                            break;
                    }
                }

                var navigationQuery = entities.EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
                while (navigationQuery.MoveNext(out _, out _, out var transform))
                {
                    if (transform.GridUid.HasValue && grids.Contains(transform.GridUid.Value))
                        navigationComputers++;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(leaderMarkers, Is.EqualTo(4), path.ToString());
                    Assert.That(memberMarkers, Is.EqualTo(8), path.ToString());
                    Assert.That(entityMarkers, Is.EqualTo(3), path.ToString());
                    Assert.That(navigationComputers, Is.GreaterThanOrEqualTo(1), path.ToString());
                });
            }
        });

        await pair.CleanReturnAsync();
    }
}
