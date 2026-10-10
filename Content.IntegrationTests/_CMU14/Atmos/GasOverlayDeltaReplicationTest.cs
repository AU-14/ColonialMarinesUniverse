#pragma warning disable RA0002 // Seed authoritative overlay chunks without running atmosphere simulation.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using static Content.Shared.Atmos.EntitySystems.SharedGasTileOverlaySystem;
using ServerGasOverlaySystem = Content.Server.Atmos.EntitySystems.GasTileOverlaySystem;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
[TestOf(typeof(ServerGasOverlaySystem))]
[EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCGasTileOverlayUpdate), false)]
public sealed class GasOverlayDeltaReplicationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task ChunkPoolsReuseTemporaryCollectionsWithoutRecyclingHistory(bool hasOverlay)
    {
        var (map, _) = await PrepareViewer();
        await Server.WaitAssertion(() =>
        {
            if (!hasOverlay)
                SEntMan.RemoveComponent<GasTileOverlayComponent>(map.Grid.Owner);

            var system = Server.System<ServerGasOverlaySystem>();
            var viewerPool = GetPrivateField<ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>>>(system, "_chunkViewerPool");
            var indexPool = GetPrivateField<ObjectPool<HashSet<Vector2i>>>(system, "_chunkIndexPool");
            var history = GetPrivateField<Dictionary<ICommonSession, Dictionary<NetEntity, HashSet<Vector2i>>>>(system, "_lastSentChunks");
            Assert.That(history[ServerSession!], Is.Empty);

            // Prime the real pools with known instances rather than inferring reuse from GC timings.
            var expectedDictionary = viewerPool.Get();
            var expectedIndices = indexPool.Get();
            Assert.That(expectedDictionary, Is.Empty);
            Assert.That(expectedIndices, Is.Empty);
            viewerPool.Return(expectedDictionary);
            indexPool.Return(expectedIndices);

            system.UpdateSessions();

            var returnedDictionary = viewerPool.Get();
            Assert.That(returnedDictionary, Is.SameAs(expectedDictionary),
                "the temporary range dictionary must return to its pool after every session update");
            Assert.That(returnedDictionary, Is.Empty);
            viewerPool.Return(returnedDictionary);

            var grid = SEntMan.GetNetEntity(map.Grid.Owner);
            var availableIndices = indexPool.Get();
            if (hasOverlay)
            {
                Assert.That(history[ServerSession!][grid], Is.SameAs(expectedIndices));
                Assert.That(expectedIndices, Does.Contain(Vector2i.Zero),
                    "clearing the temporary dictionary must not clear the sets transferred to history");
                Assert.That(availableIndices, Is.Not.SameAs(expectedIndices),
                    "a set retained by session history must not simultaneously be available in the pool");
            }
            else
            {
                Assert.That(history[ServerSession!], Does.Not.ContainKey(grid));
                Assert.That(availableIndices, Is.SameAs(expectedIndices),
                    "a grid without a gas overlay must return its untransferred range set");
            }

            Assert.That(availableIndices, Is.Empty);
            indexPool.Return(availableIndices);
        });
        await Pair.RunUntilSynced();
        await AssertNoEvents();
    }

    [Test]
    public async Task EmptyViewerReturnsTheRentedDictionary()
    {
        await PrepareViewer();
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            var system = Server.System<ServerGasOverlaySystem>();
            var pool = GetPrivateField<ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>>>(system, "_chunkViewerPool");
            var expected = pool.Get();
            Assert.That(expected, Is.Empty);
            pool.Return(expected);

            system.UpdateSessions();

            var returned = pool.Get();
            Assert.That(returned, Is.SameAs(expected));
            Assert.That(returned, Is.Empty);
            pool.Return(returned);
        });
        await Pair.RunUntilSynced();
        await AssertNoEvents();
    }

    [Test]
    public async Task EmptyGridsDoNotEmitOverlayEvents()
    {
        var (map, _) = await PrepareViewer();
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<GasTileOverlayComponent>(map.Grid.Owner).Chunks, Is.Empty));

        // A first visit still records the visible range even when it contains no gas data.
        await SendOverlayUpdate();
        await AssertNoEvents();
        await SendOverlayUpdate();
        await AssertNoEvents();
    }

    [Test]
    public async Task FirstVisitAndChangesReplicateWithoutUnchangedEvents()
    {
        var (map, _) = await PrepareViewer();
        GasOverlayChunk chunk = default!;
        NetEntity grid = default;
        await Server.WaitAssertion(() =>
        {
            grid = SEntMan.GetNetEntity(map.Grid.Owner);
            chunk = AddChunk(map.Grid.Owner, 400f);
            Assert.That(chunk.LastUpdate, Is.EqualTo(GameTick.Zero),
                "first entry must send old chunks even without a recent modification");
        });

        await SendOverlayUpdate();
        await AssertUpdatedChunk(grid, 400f);
        await ClearEvents();

        for (var i = 0; i < 3; i++)
        {
            await SendOverlayUpdate();
            await AssertNoEvents();
            await AssertClientTemperature(grid, 400f);
        }

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            SetTemperature(chunk, 800f);
            chunk.LastUpdate = SGameTiming.CurTick;
        });
        await SendOverlayUpdate();
        await AssertUpdatedChunk(grid, 800f);
        await ClearEvents();
        await SendOverlayUpdate();
        await AssertNoEvents();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LeavingAndReenteringResendsUnchangedChunks(bool leaveWholeGrid)
    {
        var (map, viewer) = await PrepareViewer();
        NetEntity grid = default;
        await Server.WaitAssertion(() =>
        {
            if (!leaveWholeGrid)
            {
                // Keep one connected grid in range at both ends, with no gas data at the far end.
                var maps = Server.System<SharedMapSystem>();
                for (var x = 1; x <= 128; x++)
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
            }

            grid = SEntMan.GetNetEntity(map.Grid.Owner);
            AddChunk(map.Grid.Owner, 400f);
        });
        await Pair.RunUntilSynced();
        await SendOverlayUpdate();
        await AssertUpdatedChunk(grid, 400f);
        await ClearEvents();

        await Server.WaitAssertion(() =>
        {
            var parent = leaveWholeGrid ? map.MapUid : map.Grid.Owner;
            Server.System<SharedTransformSystem>().SetCoordinates(viewer, new EntityCoordinates(parent, 128, 0));
        });
        await SendOverlayUpdate();
        await Client.WaitAssertion(() =>
        {
            var events = Client.System<GasOverlayDeltaProbeSystem>().Events;
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].UpdatedChunks, Is.Empty,
                "removals must not carry an empty update list for an otherwise unchanged grid");
            Assert.That(events[0].RemovedChunks, Does.ContainKey(grid));
            Assert.That(events[0].RemovedChunks[grid], Does.Contain(Vector2i.Zero));
            if (!leaveWholeGrid)
            {
                var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(grid));
                Assert.That(overlay.Chunks, Does.Not.ContainKey(Vector2i.Zero));
            }
        });
        await ClearEvents();

        // The unchanged far range must settle without sending another event.
        await SendOverlayUpdate();
        await AssertNoEvents();

        await Server.WaitAssertion(() =>
        {
            Server.System<SharedTransformSystem>().SetCoordinates(viewer, map.GridCoords);
            Assert.That(SEntMan.GetComponent<GasTileOverlayComponent>(map.Grid.Owner)
                .Chunks[Vector2i.Zero].LastUpdate, Is.EqualTo(GameTick.Zero));
        });
        // Make the grid available to the client again before sending the gas event.
        await Pair.RunUntilSynced();
        await SendOverlayUpdate();
        await AssertUpdatedChunk(grid, 400f, expectRemovals: !leaveWholeGrid);
        await ClearEvents();
        await SendOverlayUpdate();
        await AssertNoEvents();
    }

    private async Task<(TestMapData Map, EntityUid Viewer)> PrepareViewer()
    {
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CVars.NetPVS, true);
            Server.CfgMan.SetCVar(CVars.NetMaxUpdateRange, 16f);
        });
        var map = await Pair.CreateTestMap();
        EntityUid viewer = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<GasTileOverlayComponent>(map.Grid.Owner);
            viewer = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, viewer);
        });
        await Pair.RunUntilSynced();
        await ClearEvents();
        return (map, viewer);
    }

    private static T GetPrivateField<T>(ServerGasOverlaySystem system, string name)
    {
        var field = typeof(ServerGasOverlaySystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T) field!.GetValue(system)!;
    }

    private GasOverlayChunk AddChunk(EntityUid grid, float temperature)
    {
        var chunk = new GasOverlayChunk(Vector2i.Zero);
        SetTemperature(chunk, temperature);
        SEntMan.GetComponent<GasTileOverlayComponent>(grid).Chunks.Add(chunk.Index, chunk);
        return chunk;
    }

    private void SetTemperature(GasOverlayChunk chunk, float temperature)
    {
        chunk.TileData[0] = new GasOverlayData(0,
            new byte[Server.System<ServerGasOverlaySystem>().VisibleGasId.Length],
            new ThermalByte(temperature));
    }

    private async Task SendOverlayUpdate()
    {
        await Server.WaitPost(() => Server.System<ServerGasOverlaySystem>().UpdateSessions());
        await Pair.RunUntilSynced();
    }

    private Task ClearEvents() => Client.WaitPost(() => Client.System<GasOverlayDeltaProbeSystem>().Events.Clear());

    private Task AssertNoEvents() => Client.WaitAssertion(() =>
        Assert.That(Client.System<GasOverlayDeltaProbeSystem>().Events, Is.Empty,
            "an unchanged visible range must not send an empty gas-overlay event"));

    private async Task AssertUpdatedChunk(NetEntity grid, float temperature, bool expectRemovals = false)
    {
        await Client.WaitAssertion(() =>
        {
            var events = Client.System<GasOverlayDeltaProbeSystem>().Events;
            Assert.That(events, Has.Count.EqualTo(1));
            if (expectRemovals)
            {
                Assert.That(events[0].RemovedChunks, Does.ContainKey(grid));
                Assert.That(events[0].RemovedChunks[grid], Does.Not.Contain(Vector2i.Zero));
            }
            else
                Assert.That(events[0].RemovedChunks, Is.Empty);
            Assert.That(events[0].UpdatedChunks.Keys, Is.EqualTo(new[] { grid }));
            Assert.That(events[0].UpdatedChunks[grid], Has.Count.EqualTo(1));
            Assert.That(events[0].UpdatedChunks[grid][0].Index, Is.EqualTo(Vector2i.Zero));
        });
        await AssertClientTemperature(grid, temperature);
    }

    private Task AssertClientTemperature(NetEntity grid, float temperature) => Client.WaitAssertion(() =>
    {
        var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(grid));
        Assert.That(overlay.Chunks, Does.ContainKey(Vector2i.Zero));
        Assert.That(overlay.Chunks[Vector2i.Zero].TileData[0].ByteGasTemperature.TryGetTemperature(out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(temperature));
    });
}

public sealed class GasOverlayDeltaProbeSystem : EntitySystem
{
    public readonly List<GasOverlayUpdateEvent> Events = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GasOverlayUpdateEvent>(OnUpdate);
    }

    private void OnUpdate(GasOverlayUpdateEvent args)
    {
        Events.Add(args);
    }
}
