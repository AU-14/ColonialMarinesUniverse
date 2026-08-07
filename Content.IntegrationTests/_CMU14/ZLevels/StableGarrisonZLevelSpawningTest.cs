using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Roles;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
public sealed class StableGarrisonZLevelSpawningTest
{
    private const string StableGarrison = "StableGarrisonRedux";
    private static readonly ProtoId<JobPrototype> CmbMarshal = "AU14JobCivilianCMBMarshal";

    [Test]
    public async Task ZLevelSpawnsInitializeAndBelongToStation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var entities = server.EntMan;
        var maps = entities.System<SharedMapSystem>();
        var prototypes = server.ProtoMan;
        var spawning = entities.System<StationSpawningSystem>();
        var stations = entities.System<StationSystem>();
        var ticker = entities.System<GameTicker>();

        await server.WaitAssertion(() =>
        {
            var mapPrototype = prototypes.Index<GameMapPrototype>(StableGarrison);
            var options = DeserializationOptions.Default with { InitializeMaps = false };
            ticker.LoadGameMap(mapPrototype, out var mainMapId, options);
            maps.InitializeMap(mainMapId);

            var mainMap = maps.GetMap(mainMapId);
            var network = FindNetwork(entities, mainMap);
            Assert.That(network.Comp.ZLevels, Has.Count.EqualTo(7));

            var mainGrid = maps.GetAllGrids(mainMapId).Single();
            var station = stations.GetOwningStation(mainGrid.Owner);
            Assert.That(station, Is.Not.Null);

            foreach (var (depth, mapUid) in network.Comp.ZLevels)
            {
                Assert.That(mapUid, Is.Not.Null, $"Missing Stable Garrison map at depth {depth}.");
                var map = entities.GetComponent<MapComponent>(mapUid!.Value);
                Assert.That(maps.IsInitialized(map.MapId), Is.True, $"Map at depth {depth} was not initialized.");

                var grids = maps.GetAllGrids(map.MapId).ToArray();
                Assert.That(grids, Is.Not.Empty, $"Map at depth {depth} has no grids.");
                foreach (var grid in grids)
                {
                    Assert.That(
                        stations.GetOwningStation(grid.Owner),
                        Is.EqualTo(station),
                        $"Grid {grid.Owner} at depth {depth} does not belong to the Stable Garrison station.");
                }
            }

            var mob = spawning.SpawnPlayerCharacterOnStation(station, CmbMarshal, null);
            Assert.That(mob, Is.Not.Null);

            var mobMap = entities.GetComponent<TransformComponent>(mob!.Value).MapUid;
            Assert.That(mobMap, Is.EqualTo(network.Comp.ZLevels[1]), "CMB Marshal did not spawn on z=+1.");
            entities.DeleteEntity(mob.Value);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var query = entities.EntityQueryEnumerator<RandomSpawnerComponent, MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var metadata, out var xform))
            {
                if (metadata.EntityPrototype?.ID == "CMBedsheetSpawner")
                    Assert.Fail($"The z=+1 bedsheet spawner on map {xform.MapID} did not run MapInit.");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<CMUZLevelsNetworkComponent> FindNetwork(IEntityManager entities, EntityUid mainMap)
    {
        var zLevelMap = entities.GetComponent<CMUZLevelMapComponent>(mainMap);
        var network = entities.GetComponent<CMUZLevelsNetworkComponent>(zLevelMap.NetworkUid);
        return (zLevelMap.NetworkUid, network);
    }
}
