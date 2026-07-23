using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelAcousticPathTest : GameTest
{
    private const float OpeningRadius = 1.1f;
    private static readonly Vector2 SourcePosition = new(0.5f, 0.5f);

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task DownwardPathUsesCurrentFloorAndCarriesOpeningForward()
    {
        var server = Pair.Server;
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var zLevels = SEntMan.System<CMUZLevelsSystem>();
        var tileManager = server.ResolveDependency<ITileDefinitionManager>();
        var floor = new Tile(tileManager["FloorSteel"].TileId);
        var maps = new EntityUid[3];
        var mapIds = new MapId[3];
        var path = new List<CMUZLevelAcousticPathStep>();

        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < maps.Length; i++)
            {
                maps[i] = mapSystem.CreateMap(out mapIds[i]);
                var grid = SEntMan.AddComponent<MapGridComponent>(maps[i]);
                FillFloor(mapSystem, (maps[i], grid), floor);
            }

            SetOpening(mapSystem, maps[2], new Vector2i(1, 0));
            SetOpening(mapSystem, maps[1], new Vector2i(2, 0));

            Assert.That(lifecycle.TryCombineLevels(maps, out _, out var error), Is.True, error);

            var sourceMap = SEntMan.GetComponent<CMUZLevelMapComponent>(maps[2]);
            zLevels.BuildAcousticPath((maps[2], sourceMap), SourcePosition, -1, 2, OpeningRadius, path);

            Assert.That(path, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(path[0].TargetMap.Owner, Is.EqualTo(maps[1]));
                Assert.That(path[0].OpeningPosition, Is.EqualTo(new Vector2(1.5f, 0.5f)));
                Assert.That(path[1].TargetMap.Owner, Is.EqualTo(maps[0]));
                Assert.That(path[1].OpeningPosition, Is.EqualTo(new Vector2(2.5f, 0.5f)));
            });
        });

        await server.WaitPost(() =>
        {
            foreach (var mapId in mapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }
        });
    }

    [Test]
    public async Task UpwardPathUsesTargetFloorAndCarriesOpeningForward()
    {
        var server = Pair.Server;
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var zLevels = SEntMan.System<CMUZLevelsSystem>();
        var tileManager = server.ResolveDependency<ITileDefinitionManager>();
        var floor = new Tile(tileManager["FloorSteel"].TileId);
        var maps = new EntityUid[3];
        var mapIds = new MapId[3];
        var path = new List<CMUZLevelAcousticPathStep>();

        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < maps.Length; i++)
            {
                maps[i] = mapSystem.CreateMap(out mapIds[i]);
                var grid = SEntMan.AddComponent<MapGridComponent>(maps[i]);
                FillFloor(mapSystem, (maps[i], grid), floor);
            }

            SetOpening(mapSystem, maps[1], new Vector2i(1, 0));
            SetOpening(mapSystem, maps[2], new Vector2i(2, 0));

            Assert.That(lifecycle.TryCombineLevels(maps, out _, out var error), Is.True, error);

            var sourceMap = SEntMan.GetComponent<CMUZLevelMapComponent>(maps[0]);
            zLevels.BuildAcousticPath((maps[0], sourceMap), SourcePosition, 1, 2, OpeningRadius, path);

            Assert.That(path, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(path[0].TargetMap.Owner, Is.EqualTo(maps[1]));
                Assert.That(path[0].OpeningPosition, Is.EqualTo(new Vector2(1.5f, 0.5f)));
                Assert.That(path[1].TargetMap.Owner, Is.EqualTo(maps[2]));
                Assert.That(path[1].OpeningPosition, Is.EqualTo(new Vector2(2.5f, 0.5f)));
            });
        });

        await server.WaitPost(() =>
        {
            foreach (var mapId in mapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }
        });
    }

    [Test]
    public async Task SourceShutdownStopsOwnedProjection()
    {
        var server = Pair.Server;
        var audioSystem = SEntMan.System<SharedAudioSystem>();
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        var (maps, mapIds) = await CreateTwoLevelAcousticNetwork();
        EntityUid sourceAudio = default;
        EntityUid projectedAudio = default;
        EntityUid listener = default;

        await server.WaitAssertion(() =>
        {
            listener = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(maps[0], SourcePosition));
            Assert.That(playerManager.SetAttachedEntity(session, listener), Is.True);

            var played = audioSystem.PlayPvs(
                new ResolvedPathSpecifier("/Audio/Effects/alert.ogg"),
                new EntityCoordinates(maps[1], SourcePosition),
                AudioParams.Default.WithMaxDistance(10f));

            Assert.That(played, Is.Not.Null);
            sourceAudio = played.Value.Entity;
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            projectedAudio = FindProjection(sourceAudio, maps[0], listener);
            Assert.That(projectedAudio.IsValid(), Is.True, "Cross-Z audio projection was not created.");

            audioSystem.Stop(sourceAudio);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(sourceAudio), Is.False);
                Assert.That(SEntMan.EntityExists(projectedAudio), Is.False);
            });
        });

        await DeleteMaps(mapIds);
    }

    [Test]
    public async Task FilteredSourceIsNotProjected()
    {
        var server = Pair.Server;
        var audioSystem = SEntMan.System<SharedAudioSystem>();
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        var (maps, mapIds) = await CreateTwoLevelAcousticNetwork();
        EntityUid sourceAudio = default;
        EntityUid listener = default;

        await server.WaitAssertion(() =>
        {
            listener = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(maps[0], SourcePosition));
            Assert.That(playerManager.SetAttachedEntity(session, listener), Is.True);

            var played = audioSystem.PlayStatic(
                new ResolvedPathSpecifier("/Audio/Effects/alert.ogg"),
                Filter.Empty(),
                new EntityCoordinates(maps[1], SourcePosition),
                false,
                AudioParams.Default.WithMaxDistance(10f));

            Assert.That(played, Is.Not.Null);
            sourceAudio = played.Value.Entity;
            Assert.That(played.Value.Component.IncludedEntities, Is.Empty);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(
                FindProjection(sourceAudio, maps[0], listener).IsValid(),
                Is.False,
                "A filtered source leaked into an unfiltered cross-Z projection.");

            audioSystem.Stop(sourceAudio);
        });

        await server.WaitRunTicks(1);
        await DeleteMaps(mapIds);
    }

    [Test]
    public async Task LoopingSourceIsNotProjected()
    {
        var server = Pair.Server;
        var audioSystem = SEntMan.System<SharedAudioSystem>();
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        var (maps, mapIds) = await CreateTwoLevelAcousticNetwork();
        EntityUid sourceAudio = default;
        EntityUid listener = default;

        await server.WaitAssertion(() =>
        {
            listener = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(maps[0], SourcePosition));
            Assert.That(playerManager.SetAttachedEntity(session, listener), Is.True);

            var played = audioSystem.PlayPvs(
                new ResolvedPathSpecifier("/Audio/Effects/alert.ogg"),
                new EntityCoordinates(maps[1], SourcePosition),
                AudioParams.Default
                    .WithMaxDistance(10f)
                    .WithLoop(true));

            Assert.That(played, Is.Not.Null);
            sourceAudio = played.Value.Entity;
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(FindProjection(sourceAudio, maps[0], listener).IsValid(), Is.False);

            audioSystem.Stop(sourceAudio);
        });

        await server.WaitRunTicks(1);
        await DeleteMaps(mapIds);
    }

    private async Task<(EntityUid[] Maps, MapId[] MapIds)> CreateTwoLevelAcousticNetwork()
    {
        var server = Pair.Server;
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var tileManager = server.ResolveDependency<ITileDefinitionManager>();
        var floor = new Tile(tileManager["FloorSteel"].TileId);
        var maps = new EntityUid[2];
        var mapIds = new MapId[2];

        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < maps.Length; i++)
            {
                maps[i] = mapSystem.CreateMap(out mapIds[i]);
                var grid = SEntMan.AddComponent<MapGridComponent>(maps[i]);
                FillFloor(mapSystem, (maps[i], grid), floor);
            }

            SetOpening(mapSystem, maps[1], Vector2i.Zero);
            Assert.That(lifecycle.TryCombineLevels(maps, out _, out var error), Is.True, error);
        });

        return (maps, mapIds);
    }

    private EntityUid FindProjection(
        EntityUid source,
        EntityUid targetMap,
        EntityUid listener)
    {
        var query = SEntMan.EntityQueryEnumerator<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (uid == source ||
                xform.MapUid != targetMap ||
                audio.IncludedEntities?.Contains(listener) != true)
            {
                continue;
            }

            return uid;
        }

        return EntityUid.Invalid;
    }

    private async Task DeleteMaps(IEnumerable<MapId> mapIds)
    {
        var mapSystem = SEntMan.System<SharedMapSystem>();
        await Pair.Server.WaitPost(() =>
        {
            foreach (var mapId in mapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }
        });
    }

    private static void FillFloor(
        SharedMapSystem mapSystem,
        Entity<MapGridComponent> grid,
        Tile floor)
    {
        for (var x = -4; x <= 4; x++)
        {
            for (var y = -4; y <= 4; y++)
            {
                mapSystem.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), floor);
            }
        }
    }

    private void SetOpening(
        SharedMapSystem mapSystem,
        EntityUid map,
        Vector2i tile)
    {
        var grid = SEntMan.GetComponent<MapGridComponent>(map);
        mapSystem.SetTile(map, grid, tile, Tile.Empty);
    }
}
