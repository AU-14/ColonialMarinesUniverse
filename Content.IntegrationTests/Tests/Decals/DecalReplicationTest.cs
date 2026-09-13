using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared.Decals;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting;
using Robust.UnitTesting.Pool;
using ClientDecalSystem = Content.Client.Decals.DecalSystem;
using ServerDecalSystem = Content.Server.Decals.DecalSystem;

namespace Content.IntegrationTests.Tests.Decals;

[TestFixture]
[TestOf(typeof(ServerDecalSystem))]
public sealed class DecalReplicationTest
{
    private static readonly Vector2i OriginChunk = Vector2i.Zero;
    private static readonly Vector2i AdjacentChunk = new(1, 0);

    [Test]
    public async Task ChunkComponentReplicatesMutationsAndCrossChunkMoves()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitPost(() => server.CfgMan.SetCVar(CVars.NetPVS, true));
        var map = await pair.CreateTestMap();
        await PrepareAdjacentChunkAndViewer(pair, map);

        DecalIndex first = default;
        DecalIndex changed = default;
        DecalIndex removed = default;
        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(decals.TryAddDecal("burnt1", Coordinates(map.Grid.Owner, 0.1f), out first), Is.True);
                Assert.That(decals.TryAddDecal("burnt1", Coordinates(map.Grid.Owner, 0.2f), out changed), Is.True);
                Assert.That(decals.TryAddDecal("burnt1", Coordinates(map.Grid.Owner, 0.3f), out removed), Is.True);
            });
        });
        await pair.RunUntilSynced();

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
        {
            Assert.That(component.Decals.Keys, Is.EquivalentTo(new[] { first.Id, changed.Id, removed.Id }));
        });

        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.SetDecalColor(map.Grid.Owner, changed, Color.Red), Is.True);
            Assert.That(decals.RemoveDecal(map.Grid.Owner, removed), Is.True);
        });
        await pair.RunUntilSynced();

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(component.Decals.Keys, Is.EquivalentTo(new[] { first.Id, changed.Id }));
                Assert.That(component.Decals[changed.Id].Color, Is.EqualTo(Color.Red));
            });
        });

        DecalIndex moved = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var decals = entMan.System<ServerDecalSystem>();
            Assert.That(decals.SetDecalPosition(
                map.Grid.Owner,
                changed,
                Coordinates(map.Grid.Owner, 16.1f)), Is.True);

            var component = GetChunkComponent(entMan, map.Grid.Owner, AdjacentChunk);
            Assert.That(component.Decals, Has.Count.EqualTo(1));
            moved = new DecalIndex(AdjacentChunk, component.Decals.Keys.Single());
        });
        await pair.RunUntilSynced();

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
        {
            Assert.That(component.Decals.Keys, Is.EqualTo(new[] { first.Id }));
        });
        await AssertClientChunk(client, map.CGridUid, AdjacentChunk, component =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(component.Decals.Keys, Is.EqualTo(new[] { moved.Id }));
                Assert.That(component.Decals[moved.Id].Coordinates, Is.EqualTo(new Vector2(16.1f, 0.1f)));
                Assert.That(component.Decals[moved.Id].Color, Is.EqualTo(Color.Red));
            });
        });

        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.RemoveDecal(map.Grid.Owner, first), Is.True);
            Assert.That(decals.RemoveDecal(map.Grid.Owner, moved), Is.True);
        });
        await pair.RunUntilSynced();

        await AssertClientChunkMissing(client, map.CGridUid, OriginChunk);
        await AssertClientChunkMissing(client, map.CGridUid, AdjacentChunk);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredictedIdsAreReconciledWithoutDuplicateDecals()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitPost(() => server.CfgMan.SetCVar(CVars.NetPVS, true));
        var map = await pair.CreateTestMap();
        await AttachViewer(pair, map);

        DecalIndex baseline = default;
        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.TryAddDecal("burnt1", Coordinates(map.Grid.Owner, 0.1f), out baseline), Is.True);
        });
        await pair.RunUntilSynced();

        DecalIndex predicted = default;
        await client.WaitAssertion(() =>
        {
            var decals = client.EntMan.System<ClientDecalSystem>();
            var coordinates = Coordinates(map.CGridUid, 0.4f);
            var decal = new Decal(coordinates.Position, "burnt2", Color.Blue, Angle.Zero, 0, true);
            Assert.That(decals.TryAddDecal(decal, coordinates, out predicted), Is.True);
            Assert.That(predicted.Id, Is.GreaterThanOrEqualTo(DecalChunkComponent.MinPredictedDecalId));
        });

        DecalIndex authoritative = default;
        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.TryAddDecal(
                "burnt2",
                Coordinates(map.Grid.Owner, 0.4f),
                out authoritative,
                Color.Blue,
                cleanable: true), Is.True);
            Assert.That(authoritative.Id, Is.LessThanOrEqualTo(DecalChunkComponent.MaxServerDecalId));
        });
        await pair.RunUntilSynced();

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(component.Decals.Keys, Is.EquivalentTo(new[] { baseline.Id, authoritative.Id }));
                Assert.That(component.Decals, Does.Not.ContainKey(predicted.Id));
                Assert.That(component.Decals.Values.Count(decal => decal.Id == "burnt2"), Is.EqualTo(1));
                Assert.That(component.Decals[authoritative.Id].Coordinates, Is.EqualTo(new Vector2(0.4f, 0.1f)));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChunkLeavesAndReentersPvsWithLatestState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitPost(() => server.CfgMan.SetCVar(CVars.NetPVS, true));
        var map = await pair.CreateTestMap();
        var viewer = await PrepareAdjacentChunkAndViewer(pair, map);

        DecalIndex origin = default;
        DecalIndex adjacent = default;
        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.TryAddDecal("burnt1", Coordinates(map.Grid.Owner, 0.1f), out origin), Is.True);
            Assert.That(decals.TryAddDecal("burnt1", Coordinates(map.Grid.Owner, 16.1f), out adjacent), Is.True);
        });
        await pair.RunUntilSynced();

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
            Assert.That(component.Decals.Keys, Is.EqualTo(new[] { origin.Id })));
        await AssertClientChunk(client, map.CGridUid, AdjacentChunk, component =>
            Assert.That(component.Decals.Keys, Is.EqualTo(new[] { adjacent.Id })));

        await server.WaitPost(() =>
        {
            var transform = server.EntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(viewer, Coordinates(map.Grid.Owner, 1000f));
        });
        await pair.RunTicksSync(20);

        await AssertClientChunkMissing(client, map.CGridUid, OriginChunk);
        await AssertClientChunkMissing(client, map.CGridUid, AdjacentChunk);

        DecalIndex addedWhileDetached = default;
        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.SetDecalColor(map.Grid.Owner, origin, Color.Green), Is.True);
            Assert.That(decals.TryAddDecal(
                "burnt2",
                Coordinates(map.Grid.Owner, 0.4f),
                out addedWhileDetached), Is.True);

            var transform = server.EntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(viewer, Coordinates(map.Grid.Owner, 8f));
        });
        await pair.RunTicksSync(20);

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(component.Decals.Keys, Is.EquivalentTo(new[] { origin.Id, addedWhileDetached.Id }));
                Assert.That(component.Decals[origin.Id].Color, Is.EqualTo(Color.Green));
                Assert.That(component.Decals.Values.Count(decal => decal.Coordinates == new Vector2(0.4f, 0.1f)),
                    Is.EqualTo(1));
            });
        });
        await AssertClientChunk(client, map.CGridUid, AdjacentChunk, component =>
            Assert.That(component.Decals.Keys, Is.EqualTo(new[] { adjacent.Id })));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BulkRemovalFiltersAndPreservesNonCleanableDecals()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitPost(() => server.CfgMan.SetCVar(CVars.NetPVS, true));
        var map = await pair.CreateTestMap();
        await AttachViewer(pair, map);

        DecalIndex cleanableMatch = default;
        DecalIndex protectedMatch = default;
        DecalIndex differentId = default;
        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            Assert.That(decals.TryAddDecal(
                "burnt1",
                Coordinates(map.Grid.Owner, 0.1f),
                out cleanableMatch,
                cleanable: true), Is.True);
            Assert.That(decals.TryAddDecal(
                "burnt1",
                Coordinates(map.Grid.Owner, 0.2f),
                out protectedMatch,
                cleanable: false), Is.True);
            Assert.That(decals.TryAddDecal(
                "burnt2",
                Coordinates(map.Grid.Owner, 0.3f),
                out differentId,
                cleanable: true), Is.True);
        });
        await pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            var result = decals.RemoveDecals(
                map.Grid.Owner,
                new HashSet<string> { "burnt1" },
                cleanableOnly: true);
            Assert.Multiple(() =>
            {
                Assert.That(result.Removed, Is.EqualTo(1));
                Assert.That(result.Skipped, Is.EqualTo(1));
            });
        });
        await pair.RunUntilSynced();

        await AssertClientChunk(client, map.CGridUid, OriginChunk, component =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(component.Decals.Keys, Is.EquivalentTo(new[] { protectedMatch.Id, differentId.Id }));
                Assert.That(component.Decals, Does.Not.ContainKey(cleanableMatch.Id));
                Assert.That(component.Decals[protectedMatch.Id].Cleanable, Is.False);
                Assert.That(component.Decals[differentId.Id].Id, Is.EqualTo("burnt2"));
            });
        });

        await server.WaitAssertion(() =>
        {
            var decals = server.EntMan.System<ServerDecalSystem>();
            var filtered = decals.RemoveDecals(
                map.Grid.Owner,
                new HashSet<string> { "burnt2" },
                cleanableOnly: false);
            Assert.Multiple(() =>
            {
                Assert.That(filtered.Removed, Is.EqualTo(1));
                Assert.That(filtered.Skipped, Is.Zero);
            });

            var all = decals.RemoveDecals(map.Grid.Owner, cleanableOnly: false);
            Assert.Multiple(() =>
            {
                Assert.That(all.Removed, Is.EqualTo(1));
                Assert.That(all.Skipped, Is.Zero);
            });
        });
        await pair.RunUntilSynced();

        await AssertClientChunkMissing(client, map.CGridUid, OriginChunk);
        await pair.CleanReturnAsync();
    }

    private static EntityCoordinates Coordinates(EntityUid grid, float x, float y = 0.1f)
        => new(grid, new Vector2(x, y));

    private static async Task<EntityUid> PrepareAdjacentChunkAndViewer(TestPair pair, TestMapData map)
    {
        await pair.Server.WaitPost(() =>
        {
            var mapSystem = pair.Server.EntMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(16, 0), map.Tile.Tile);
        });
        return await AttachViewer(pair, map);
    }

    private static async Task<EntityUid> AttachViewer(TestPair pair, TestMapData map)
    {
        EntityUid viewer = default;
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var player = server.PlayerMan.Sessions.Single();
            viewer = server.EntMan.SpawnEntity(null, Coordinates(map.Grid.Owner, 8f));
            server.PlayerMan.SetAttachedEntity(player, viewer);
            Assert.That(player.AttachedEntity, Is.EqualTo(viewer));
        });
        await pair.RunTicksSync(5);
        return viewer;
    }

    private static DecalChunkComponent GetChunkComponent(
        IEntityManager entMan,
        EntityUid grid,
        Vector2i chunkIndices)
    {
        var chunks = entMan.System<ChunkEntitySystem>();
        Assert.That(chunks.TryGetChunk(grid, chunkIndices, out var chunk), Is.True);
        return entMan.GetComponent<DecalChunkComponent>(chunk!.Value.Owner);
    }

    private static async Task AssertClientChunk(
        RobustIntegrationTest.ClientIntegrationInstance client,
        EntityUid grid,
        Vector2i chunkIndices,
        Action<DecalChunkComponent> assertion)
    {
        await client.WaitAssertion(() =>
        {
            var component = GetChunkComponent(client.EntMan, grid, chunkIndices);
            assertion(component);
        });
    }

    private static async Task AssertClientChunkMissing(
        RobustIntegrationTest.ClientIntegrationInstance client,
        EntityUid grid,
        Vector2i chunkIndices)
    {
        await client.WaitAssertion(() =>
        {
            var chunks = client.EntMan.System<ChunkEntitySystem>();
            if (!chunks.TryGetChunk(grid, chunkIndices, out var chunk))
                return;

            Assert.That(client.EntMan.HasComponent<DecalChunkComponent>(chunk!.Value.Owner), Is.False);
        });
    }
}
