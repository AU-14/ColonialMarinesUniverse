using Content.Shared._RMC14.Areas;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Areas;

[TestFixture]
public sealed class AreasCommandTest
{
    private static readonly EntProtoId<AreaComponent> TestArea = "RMCAreaSpace";

    [Test]
    public async Task CleanupRemovesOnlyEntitiesAndAreasOnEmptyTiles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid entityOnTile = default;
        EntityUid entityOnEmptyTile = default;
        EntityUid areaOnTile = default;
        EntityUid areaOnEmptyTile = default;
        var tileIndices = new Vector2i(1, 0);
        var emptyIndices = Vector2i.Zero;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, tileIndices, map.Tile.Tile);
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, emptyIndices, Tile.Empty);

            var areaGrid = entities.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            var areaSystem = server.System<AreaSystem>();
            areaSystem.ReplaceArea(areaGrid, tileIndices, TestArea);
            areaSystem.ReplaceArea(areaGrid, emptyIndices, TestArea);

            entityOnTile = entities.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, tileIndices));
            entityOnEmptyTile = entities.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, emptyIndices));
            areaOnTile = entities.SpawnEntity(TestArea, new EntityCoordinates(map.Grid.Owner, tileIndices));
            areaOnEmptyTile = entities.SpawnEntity(TestArea, new EntityCoordinates(map.Grid.Owner, emptyIndices));
        });

        await pair.WaitCommand("areas:cleanup");

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.EntityExists(entityOnTile), Is.True);
                Assert.That(entities.EntityExists(areaOnTile), Is.True);
                Assert.That(entities.EntityExists(entityOnEmptyTile), Is.False);
                Assert.That(entities.EntityExists(areaOnEmptyTile), Is.False);
                Assert.That(entities.EntityExists(map.Grid.Owner), Is.True);
                Assert.That(entities.EntityExists(map.MapUid), Is.True);
            });

            var areaGrid = entities.GetComponent<AreaGridComponent>(map.Grid.Owner);
            var grid = entities.GetComponent<MapGridComponent>(map.Grid.Owner);
            var areaSystem = server.System<AreaSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(areaSystem.TryGetArea((map.Grid.Owner, grid, areaGrid), tileIndices, out _, out _), Is.True);
                Assert.That(areaSystem.TryGetArea((map.Grid.Owner, grid, areaGrid), emptyIndices, out _, out _), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
