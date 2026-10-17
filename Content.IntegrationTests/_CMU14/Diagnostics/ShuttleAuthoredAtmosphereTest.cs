using Content.Shared.Atmos.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class ShuttleAuthoredAtmosphereTest
{
    [Test]
    public async Task AdminFaxRoomsLoadWithAuthoredAir()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/CMU14/Admin/adminfaxhub.yml"), out var map, out var grids), Is.True);
            Assert.That(grids, Has.Count.EqualTo(1));
            var atmosphere = entities.GetComponent<GridAtmosphereComponent>(grids!.Single().Owner);
            Assert.That(atmosphere.Tiles.Values.Any(tile => tile.Air is { TotalMoles: > 1 }), Is.True);
            entities.DeleteEntity(map!.Value.Owner);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PassengerShuttlesLoadWithAuthoredAir()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var loader = entities.System<MapLoaderSystem>();
            foreach (var path in new[]
                     {
                         "/Maps/_RMC14/Shuttles/escape_pod_north.yml",
                         "/Maps/_RMC14/Shuttles/escape_pod_south.yml",
                         "/Maps/_RMC14/Shuttles/escape_pod_east.yml",
                         "/Maps/_RMC14/Shuttles/escape_pod_east_liaison.yml",
                         "/Maps/_RMC14/Shuttles/escape_pod_west.yml",
                         "/Maps/_RMC14/Shuttles/lifeboat.yml",
                         "/Maps/CMU14/Vehicles/Dropships/rmc_ert_response_shuttle.yml",
                         "/Maps/CMU14/Vehicles/Dropships/rmc_ert_pmc_shuttle.yml",
                     })
            {
                var map = maps.CreateMap(out var mapId);
                Assert.That(loader.TryLoadGrid(mapId, new ResPath(path), out var loaded), Is.True, path);
                var atmosphere = entities.GetComponent<GridAtmosphereComponent>(loaded!.Value.Owner);
                Assert.That(atmosphere.Tiles.Values.Any(tile => tile.Air is { TotalMoles: > 1 }), Is.True,
                    $"{path} must supply passenger air without a runtime fixgridatmos command.");
                entities.DeleteEntity(map);
            }
        });
        await pair.CleanReturnAsync();
    }
}
