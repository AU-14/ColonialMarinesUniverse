using System.Numerics;
using Content.Server.FootPrint;
using Content.Shared.Decals;
using Content.Shared.FootPrint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FootPrint;

[TestFixture]
[TestOf(typeof(FootPrintsSystem))]
public sealed class FootPrintsSystemTest
{
    private const string FootPrintTestAliveMoverId = "FootPrintTestAliveMover";
    private const string FootPrintTestCriticalMoverId = "FootPrintTestCriticalMover";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {FootPrintTestAliveMoverId}
  components:
  - type: FootPrints
  - type: MobThresholds
    currentThresholdState: Alive
    thresholds:
      0: Alive

- type: entity
  id: {FootPrintTestCriticalMoverId}
  components:
  - type: FootPrints
  - type: MobThresholds
    currentThresholdState: Critical
    thresholds:
      0: Alive
";

    [Test]
    public async Task StainedWalkingDoesNotSpawnFootprintEntitiesButDraggingSpawnsDecal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapManager = server.ResolveDependency<IMapManager>();
        var tileDefinitionManager = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            map.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            entMan.EnsureComponent<DecalGridComponent>(grid.Owner);

            var floorTile = new Tile(tileDefinitionManager["FloorSteel"].TileId);
            for (var x = 0; x < 3; x++)
                map.SetTile(grid, new Vector2i(x, 0), floorTile);

            var aliveMover = entMan.SpawnEntity(FootPrintTestAliveMoverId, map.GridTileToLocal(grid, grid.Comp, Vector2i.Zero));
            var criticalMover = entMan.SpawnEntity(FootPrintTestCriticalMoverId, map.GridTileToLocal(grid, grid.Comp, new Vector2i(0, 0)));
            var alivePrints = entMan.GetComponent<FootPrintsComponent>(aliveMover);
            var criticalPrints = entMan.GetComponent<FootPrintsComponent>(criticalMover);

            try
            {
                SetStainedSteps(alivePrints);
                SetStainedSteps(criticalPrints);

                xform.SetLocalPosition(aliveMover, new Vector2(1.5f, 0.5f));
                Assert.That(CountFootprints(entMan, aliveMover), Is.EqualTo(0));

                xform.SetLocalPosition(criticalMover, new Vector2(1.5f, 0.5f));
                Assert.Multiple(() =>
                {
                    Assert.That(CountFootprints(entMan, criticalMover), Is.EqualTo(0));
                    Assert.That(CountDecals(entMan, grid.Owner), Is.EqualTo(1));
                });
            }
            finally
            {
                entMan.DeleteEntity(aliveMover);
                entMan.DeleteEntity(criticalMover);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void SetStainedSteps(FootPrintsComponent prints)
    {
        prints.PrintsColor = Color.Red;
        prints.StepSize = 0.1f;
        prints.DragSize = 0.1f;
    }

    private static int CountFootprints(IEntityManager entMan, EntityUid owner)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<FootPrintComponent>();
        while (query.MoveNext(out _, out var footprint))
        {
            if (footprint.PrintOwner == owner)
                count++;
        }

        return count;
    }

    private static int CountDecals(IEntityManager entMan, EntityUid grid)
    {
        var count = 0;
        var decals = entMan.GetComponent<DecalGridComponent>(grid);
        foreach (var chunk in decals.ChunkCollection.ChunkCollection.Values)
        {
            count += chunk.Decals.Count;
        }

        return count;
    }
}
