using System.Collections;
using System.Reflection;
using Content.Server.Atmos.Components;
using Content.Server.Explosion.Components;
using Content.Shared.Atmos;
using Content.Shared.Explosion.Components;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NPC.Pathfinding;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class ExplosionPreparationTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Fresh = TestContext.CurrentContext.Test.MethodName == nameof(AirtightSnapshotRetainsRecycledTolerancesAndDirectionOnlyEdits),
    };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type SystemType = typeof(ExplosionSystem);

    // These fixtures deliberately mutate blocker inputs before asking their owner to rebuild them.
#pragma warning disable RA0002
    [Test]
    public async Task AirtightSnapshotRetainsRecycledTolerancesAndDirectionOnlyEdits()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<ExplosionSystem>();
            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords);
            SEntMan.EnsureComponent<Content.Shared.Damage.Components.InjurableComponent>(wall);
            var resistance = SEntMan.EnsureComponent<ExplosionResistanceComponent>(wall);
            resistance.DamageCoefficient = 0.1371f;
            system.UpdateAirtightMap(map.Grid, Vector2i.Zero);
            var tiles = SEntMan.GetComponent<ExplosionAirtightGridComponent>(map.Grid).Tiles;
            var first = tiles.Capture();
            var original = first[Vector2i.Zero];
            var expected = original.Tolerances.Values.ToArray();
            resistance.DamageCoefficient = 0.21719f;
            system.UpdateAirtightMap(map.Grid, Vector2i.Zero);
            var changed = tiles[Vector2i.Zero];
            Assert.That(changed.ToleranceCacheIndex, Is.EqualTo(original.ToleranceCacheIndex), "Exercise actual cache-slot reuse.");
            Assert.That(changed.Tolerances.Values, Is.Not.EqualTo(expected));
            Assert.That(first[Vector2i.Zero].Tolerances.Values, Is.EqualTo(expected));
            var second = tiles.Capture();
            SEntMan.GetComponent<AirtightComponent>(wall).CurrentAirBlockedDirection = (int) AtmosDirection.North;
            system.UpdateAirtightMap(map.Grid, Vector2i.Zero);
            Assert.That(tiles[Vector2i.Zero].BlockedDirections, Is.EqualTo(AtmosDirection.North));
            Assert.That(second[Vector2i.Zero].BlockedDirections, Is.EqualTo(changed.BlockedDirections));
            Assert.That(tiles[Vector2i.Zero].Tolerances.Values, Is.EqualTo(changed.Tolerances.Values));
        });
    }
#pragma warning restore RA0002

    [Test]
    public async Task SmallUnobstructedBlastHasIndependentGoldenIntensityAndTiles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++) maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var epicenter = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var result = ((int Area, List<float> Intensities, ExplosionSpaceTileFlood? Space,
                Dictionary<EntityUid, ExplosionGridTileFlood> Grids, Matrix3x2 Matrix))
                SystemType.GetMethod("GetExplosionTiles", Private)!.Invoke(Server.System<ExplosionSystem>(),
                    new object[] { epicenter, "RMC", 12f, 6f, 100f })!;
            // Three half-distance intensity increments reach 9 at the origin. The
            // remaining 3 is distributed across the four cardinal neighbors.
            // The legacy area counter excludes the final partially-funded ring.
            // Pin that existing behavior separately from the five actual affected tiles.
            Assert.That(result.Area, Is.EqualTo(1));
            // The empty first iteration also gains an increment, at zero energy cost.
            Assert.That(result.Intensities, Is.EqualTo(new[] { 9f, 6f, 0.75f }));
            Assert.That(result.Space, Is.Null);
            Assert.That(result.Grids[map.Grid].TileLists.Keys, Is.EquivalentTo(new[] { 0, 2 }));
            Assert.That(result.Grids[map.Grid].TileLists[0], Is.EqualTo(new[] { Vector2i.Zero }));
            Assert.That(result.Grids[map.Grid].TileLists[2], Is.EquivalentTo(new[]
                { new Vector2i(0, 1), new Vector2i(1, 0), new Vector2i(0, -1), new Vector2i(-1, 0) }));
            Assert.That(result.Grids[map.Grid].TileLists.Sum(row => row.Value.Count * result.Intensities[row.Key]), Is.EqualTo(12));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SingleTileBlastSkipsGridEdgePreparation(bool inSpace)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<ExplosionSystem>();
            var maps = Server.System<SharedMapSystem>();
            // A long exposed boundary makes accidental edge setup visible as yielded work.
            for (var x = 0; x < 200; x++) maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var coordinates = new EntityCoordinates(map.Grid, inSpace ? new Vector2(0.5f, 1.5f) : new Vector2(0.5f));
            var epicenter = Server.System<SharedTransformSystem>().ToMapCoordinates(coordinates);
            var preparationType = SystemType.GetNestedType("ExplosionPreparation", BindingFlags.NonPublic)!;
            var preparation = Activator.CreateInstance(preparationType, true)!;
            var steps = ((IEnumerable) SystemType.GetMethod("PrepareExplosionTiles", Private)!
                .Invoke(system, new object[] { epicenter, "RMC", 2f, 6f, 2f, preparation })!).GetEnumerator();
            try { Assert.That(steps.MoveNext(), Is.False, "Single-tile preparation must not walk unrelated grid edges."); }
            finally { (steps as IDisposable)?.Dispose(); }
            var result = ((int Area, List<float> Intensities, ExplosionSpaceTileFlood? Space,
                Dictionary<EntityUid, ExplosionGridTileFlood> Grids, Matrix3x2 Matrix))
                preparationType.GetField("Result")!.GetValue(preparation)!;
            Assert.That(result.Area, Is.EqualTo(1));
            Assert.That(result.Intensities, Is.EqualTo(new[] { 2f }));
            if (inSpace)
            {
                Assert.That(result.Grids, Is.Empty);
                Assert.That(result.Space!.TileLists[0], Has.Count.EqualTo(1));
                Assert.That(result.Space.TileSize, Is.EqualTo(map.Grid.Comp.TileSize));
            }
            else
            {
                Assert.That(result.Space, Is.Null);
                Assert.That(result.Grids[map.Grid].TileLists[0], Is.EqualTo(new[] { Vector2i.Zero }));
            }
        });
    }

    [TestCase(1)]
    [TestCase(37)]
    public async Task ResumedFloodPreservesTileOrderAndIntensity(int batchSize)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<ExplosionSystem>();
            var maps = Server.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var epicenter = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var args = new object[] { epicenter, "RMC", 50000f, 6f, 100f };
            var expected = SystemType.GetMethod("GetExplosionTiles", Private)!.Invoke(system, args)!;
            var preparationType = SystemType.GetNestedType("ExplosionPreparation", BindingFlags.NonPublic)!;
            var preparation = Activator.CreateInstance(preparationType, true)!;
            var steps = ((IEnumerable) SystemType.GetMethod("PrepareExplosionTiles", Private)!
                .Invoke(system, args.Append(preparation).ToArray())!).GetEnumerator();
            var count = 0;
            var pauses = 0;
            try
            {
                var running = true;
                while (running)
                {
                    for (var i = 0; i < batchSize; i++)
                    {
                        if (!steps.MoveNext()) { running = false; break; }
                        count++;
                    }
                    pauses++;
                }
            }
            finally { (steps as IDisposable)?.Dispose(); }
            Assert.That(count, Is.GreaterThan(500), "The test must exercise inner flood/edge continuation.");
            Assert.That(pauses, Is.GreaterThan(1));
            var actual = preparationType.GetField("Result")!.GetValue(preparation)!;
            Assert.That(Describe(actual), Is.EqualTo(Describe(expected)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LargePreparationYieldsAndMapDeletionReleasesPausedSystems(bool removeGridFirst)
    {
        var map = await Pair.CreateTestMap();
        var config = Server.ResolveDependency<IConfigurationManager>();
        var previous = config.GetCVar(CCVars.ExplosionMaxProcessingTime);
        try
        {
            await Server.WaitAssertion(() =>
            {
                config.SetCVar(CCVars.ExplosionMaxProcessingTime, 100000f);
                var system = Server.System<ExplosionSystem>();
                var epicenter = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
                system.QueueExplosion(epicenter, "RMC", 1000000f, 6f, 100f,
                    cause: null, maxTileBreak: 0, canCreateVacuum: false);
                // A large time budget isolates the deterministic work cap from machine speed.
                Assert.That(SystemType.GetMethod("AdvancePreparation", Private)!.Invoke(system, null), Is.False);
                Assert.That(SystemType.GetField("_preparingExplosion", Private)!.GetValue(system), Is.Not.Null);
                var nodes = Server.System<NodeGroupSystem>();
                var paths = Server.System<PathfindingSystem>();
                nodes.PauseUpdating = true;
                paths.PauseUpdating = true;
                var mapEntity = SEntMan.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
                if (removeGridFirst)
                {
                    SEntMan.DeleteEntity(map.Grid.Owner);
                    Assert.That(SystemType.GetField("_preparationInvalidated", Private)!.GetValue(system), Is.True);
                    Assert.That(SystemType.GetMethod("AdvancePreparation", Private)!.Invoke(system, null), Is.False);
                    Assert.That(SystemType.GetField("_preparationInvalidated", Private)!.GetValue(system), Is.False);
                }
                SEntMan.DeleteEntity(mapEntity);
                Assert.That(SystemType.GetField("_preparingExplosion", Private)!.GetValue(system), Is.Null);
                Assert.That(nodes.PauseUpdating, Is.False);
                Assert.That(paths.PauseUpdating, Is.False);
                system.Update(0);
            });
        }
        finally { await Server.WaitPost(() => config.SetCVar(CCVars.ExplosionMaxProcessingTime, previous)); }
    }

    private static string Describe(object result)
    {
        var value = ((int Area, List<float> Intensities, ExplosionSpaceTileFlood? Space,
            Dictionary<EntityUid, ExplosionGridTileFlood> Grids, Matrix3x2 Matrix)) result;
        string Tiles(ExplosionTileFlood flood) => string.Join(";", flood.TileLists.OrderBy(e => e.Key)
            .Select(e => $"{e.Key}:{string.Join(',', e.Value)}"));
        return $"{value.Area}|{string.Join(',', value.Intensities)}|{value.Matrix}|" +
               (value.Space == null ? "" : Tiles(value.Space)) + "|" +
               string.Join("|", value.Grids.OrderBy(e => e.Key.Id).Select(e => $"{e.Key}:{Tiles(e.Value)}"));
    }
}
