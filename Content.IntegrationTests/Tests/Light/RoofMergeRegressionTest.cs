using Content.IntegrationTests.Fixtures;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared.Light.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Light;

[TestFixture]
[TestOf(typeof(RoofSystem))]
public sealed class RoofMergeRegressionTest : GameTest
{
    private static readonly EntProtoId<AreaComponent> OpenArea = "RoofMergeOpenArea";
    private static readonly EntProtoId<AreaComponent> BlockedArea = "RoofMergeBlockedArea";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: RoofMergeOpenArea
  components:
  - type: Area
    weatherEnabled: true

- type: entity
  id: RoofMergeBlockedArea
  components:
  - type: Area
    weatherEnabled: false
""";

    [Test]
    public async Task ExplicitAndEntityRoofsTakePriorityOverAreaFallback()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        var gridNet = server.EntMan.GetNetEntity(map.Grid.Owner);
        var indices = map.Tile.GridIndices;
        var gridColor = new Color(0.1f, 0.2f, 0.3f);
        var entityColor = new Color(0.7f, 0.4f, 0.2f);
        EntityUid marker = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var areas = server.System<AreaSystem>();
            var roofs = server.System<RoofSystem>();
            var grid = entities.GetComponent<MapGridComponent>(map.Grid.Owner);
            var roof = entities.EnsureComponent<RoofComponent>(map.Grid.Owner);
            roof.Color = gridColor;
            var areaGrid = entities.EnsureComponent<AreaGridComponent>(map.Grid.Owner);

            areas.ReplaceArea(areaGrid, indices, OpenArea);
            marker = entities.SpawnEntity(null,
                new EntityCoordinates(map.Grid.Owner, indices.X + 0.5f, indices.Y + 0.5f));
            var entityRoof = entities.EnsureComponent<IsRoofComponent>(marker);
            entityRoof.Enabled = false;
            entityRoof.Color = entityColor;

            Assert.Multiple(() =>
            {
                Assert.That(areas.IsLightBlocked((map.Grid.Owner, grid), indices), Is.False);
                Assert.That(roofs.IsRooved((map.Grid.Owner, grid, roof), indices), Is.False,
                    "IsRooved must ignore both disabled roof entities and the Area fallback.");
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices), Is.Null);
            });

            areas.ReplaceArea(areaGrid, indices, BlockedArea);
            Assert.Multiple(() =>
            {
                Assert.That(areas.IsLightBlocked((map.Grid.Owner, grid), indices), Is.True);
                Assert.That(roofs.IsRooved((map.Grid.Owner, grid, roof), indices), Is.False,
                    "The RMC Area fallback belongs only to GetColor.");
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices), Is.EqualTo(gridColor));
            });

            entityRoof.Enabled = true;
            Assert.Multiple(() =>
            {
                Assert.That(roofs.IsRooved((map.Grid.Owner, grid, roof), indices), Is.True);
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices), Is.EqualTo(entityColor),
                    "An enabled IsRoof entity must win before the Area fallback.");
            });

            roofs.SetRoof((map.Grid.Owner, grid, roof), indices, true);
            Assert.Multiple(() =>
            {
                Assert.That(roofs.IsRooved((map.Grid.Owner, grid, roof), indices), Is.True);
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices), Is.EqualTo(gridColor),
                    "An explicit tile roof bit must win over an IsRoof entity color.");
                Assert.That(IsRoofBitSet(roof, indices), Is.True);
            });
        });

        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var clientGrid = client.EntMan.GetEntity(gridNet);
            var roof = client.EntMan.GetComponent<RoofComponent>(clientGrid);
            Assert.That(IsRoofBitSet(roof, indices), Is.True,
                "SetRoof must dirty and replicate the changed bitmask.");
        });

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var grid = entities.GetComponent<MapGridComponent>(map.Grid.Owner);
            var roof = entities.GetComponent<RoofComponent>(map.Grid.Owner);
            var roofs = server.System<RoofSystem>();
            var entityRoof = entities.GetComponent<IsRoofComponent>(marker);

            roofs.SetRoof((map.Grid.Owner, grid, roof), indices, false);
            Assert.Multiple(() =>
            {
                Assert.That(IsRoofBitSet(roof, indices), Is.False);
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices), Is.EqualTo(entityColor));
            });

            entityRoof.Enabled = false;
            Assert.Multiple(() =>
            {
                Assert.That(roofs.IsRooved((map.Grid.Owner, grid, roof), indices), Is.False);
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices), Is.EqualTo(gridColor),
                    "A disabled entity must be ignored before the blocked-Area fallback.");
            });
        });

        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var clientGrid = client.EntMan.GetEntity(gridNet);
            var roof = client.EntMan.GetComponent<RoofComponent>(clientGrid);
            Assert.That(IsRoofBitSet(roof, indices), Is.False,
                "Clearing a roof bit must dirty and replicate the changed bitmask.");
        });

        await pair.CleanReturnAsync();
    }

    private static bool IsRoofBitSet(RoofComponent roof, Vector2i indices)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(indices, RoofComponent.ChunkSize);
        if (!roof.Data.TryGetValue(chunkOrigin, out var chunkData))
            return false;

        var relative = SharedMapSystem.GetChunkRelative(indices, RoofComponent.ChunkSize);
        var bit = (ulong) 1 << (relative.X + relative.Y * RoofComponent.ChunkSize);
        return (chunkData & bit) == bit;
    }
}
