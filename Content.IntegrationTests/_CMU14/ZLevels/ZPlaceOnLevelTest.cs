using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Shared.CCVar;
using Content.Shared.CMU14.SavedBuilds;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>ARCH-04: <see cref="ZLevelBuildingSystem.TryPlaceStructureOnLevel"/>.</summary>
[TestFixture]
public sealed class ZPlaceOnLevelTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // The five-second collapse warning plus scheduling slack, as TileApplierSystemTest uses.
    private const int CollapseTicks = 400;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestPlacement2x2
  components:
  - type: Transform
    anchored: true
  - type: ZFootprint
    size: 2,2

- type: entity
  id: CMUTestPlacementOpening
  components:
  - type: Transform
    anchored: true
  - type: CMUZLevelFallThrough
";

    [Test]
    public async Task AuthoredWallOverOpenAirNeverCollapses()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wall = default;
        EntityUid upper = default;
        EntityUid upperGrid = default;
        Vector2i tile = default;
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var maps = Server.System<SharedMapSystem>();
            var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, 1, "CMWallMetal", new ZPlacementOptions(), out wall), Is.True);

            var xform = SEntMan.GetComponent<TransformComponent>(wall);
            upper = SEntMan.GetComponent<CMUZLevelMapComponent>(map.MapUid).MapAbove!.Value;
            upperGrid = xform.GridUid!.Value;
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            tile = maps.TileIndicesFor(upperGrid, gridComp, xform.Coordinates);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(upper), "The wall must stand on the level above.");
                Assert.That(xform.Anchored, Is.True);
                Assert.That(maps.GetTileRef(upperGrid, gridComp, tile).Tile.IsEmpty, Is.False, "The floor must exist under it.");
                Assert.That(SEntMan.HasComponent<StructuralSupportComponent>(wall), Is.False, "Authored is never enrolled.");
                Assert.That(SEntMan.HasComponent<PlayerBuiltComponent>(wall), Is.False, "Authored is never player-built.");
                Assert.That(maps.GetAnchoredEntities(upperGrid, gridComp, tile)
                    .Any(uid => SEntMan.HasComponent<TileFloorSupportComponent>(uid)), Is.False, "Authored floors carry no marker.");
            });
        });

        await Server.WaitRunTicks(CollapseTicks);

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(wall), Is.True, "An authored wall over open air must never collapse.");
                Assert.That(SEntMan.GetComponent<TransformComponent>(wall).MapUid, Is.EqualTo(upper));
                Assert.That(maps.GetTileRef(upperGrid, gridComp, tile).Tile.IsEmpty, Is.False);
                Assert.That(Server.System<ZLevelSupportSystem>().PendingCollapseCount(upperGrid), Is.Zero);
            });
            SEntMan.DeleteEntity(upper);
        });
    }

    [Test]
    public async Task CollapsibleWallHoldsOverItsSupportAndFallsWithoutIt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid beam = default;
        EntityUid wall = default;
        EntityUid upper = default;
        EntityUid upperGrid = default;
        Vector2i tile = default;
        var collapsible = new ZPlacementOptions { Mode = ZSupportMode.Collapsible };
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var maps = Server.System<SharedMapSystem>();
            var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            // The support on the ground level, enrolled through the same API.
            Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, 0, "AU14NavalisSupportBeamGreen1Tile", collapsible, out beam), Is.True);
            Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, 1, "CMWallMetal", collapsible, out wall), Is.True);

            var xform = SEntMan.GetComponent<TransformComponent>(wall);
            upper = xform.MapUid!.Value;
            upperGrid = xform.GridUid!.Value;
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            tile = maps.TileIndicesFor(upperGrid, gridComp, xform.Coordinates);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StructuralSupportComponent>(wall), Is.True, "Collapsible enrolls the wall.");
                Assert.That(maps.GetAnchoredEntities(upperGrid, gridComp, tile)
                    .Any(uid => SEntMan.HasComponent<TileFloorSupportComponent>(uid)), Is.True,
                    "A new upper floor carries a support marker, as the tile applier lays it.");
            });
        });

        await Server.WaitRunTicks(CollapseTicks);

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(wall), Is.True, "The wall over its support must hold.");
                Assert.That(SEntMan.GetComponent<StructuralSupportComponent>(wall).Supported, Is.True);
                Assert.That(maps.GetTileRef(upperGrid, gridComp, tile).Tile.IsEmpty, Is.False);
            });
            SEntMan.DeleteEntity(beam);
        });

        await Server.WaitRunTicks(CollapseTicks);

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            Assert.Multiple(() =>
            {
                Assert.That(!SEntMan.EntityExists(wall) || SEntMan.GetComponent<TransformComponent>(wall).MapUid != upper, Is.True,
                    "Without its support the collapsible wall must come down.");
                Assert.That(maps.GetTileRef(upperGrid, gridComp, tile).Tile.IsEmpty, Is.True,
                    "Its collapsible floor must go with it.");
            });
            SEntMan.DeleteEntity(upper);
        });
    }

    [Test]
    public async Task DownwardPlacementObeysTheDigPolicy()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var maps = Server.System<SharedMapSystem>();
            var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            var mapCount = SEntMan.Count<MapComponent>();
            try
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
                Assert.Multiple(() =>
                {
                    Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, -1, "CMWallMetal", new ZPlacementOptions(), out var placed), Is.False,
                        "A placement below must not dig a new level while digging down is disallowed.");
                    Assert.That(placed, Is.EqualTo(EntityUid.Invalid));
                    Assert.That(SEntMan.Count<MapComponent>(), Is.EqualTo(mapCount), "No map may be created.");
                    Assert.That(SEntMan.HasComponent<CMUZLevelMapComponent>(map.MapUid), Is.False,
                        "Not even a z-network may be bootstrapped.");
                });
            }
            finally
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
            }
        });
    }

    [Test]
    public async Task DownwardPlacementOntoAnExistingAuthoredLevelIgnoresTheDigPolicy()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var maps = Server.System<SharedMapSystem>();
            var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, world, out var lower, out _), Is.True);
            SEntMan.EnsureComponent<ZAuthoredLevelComponent>(lower);
            var mapCount = SEntMan.Count<MapComponent>();
            try
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
                // Placing into the void of a level that already exists digs nothing.
                Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, -1, "CMWallMetal", new ZPlacementOptions(), out var placed), Is.True,
                    "The dig policy refuses new levels below, not placement onto an existing authored level.");
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<TransformComponent>(placed).MapUid, Is.EqualTo(lower));
                    Assert.That(SEntMan.Count<MapComponent>(), Is.EqualTo(mapCount), "No map may be created.");
                    Assert.That(SEntMan.HasComponent<ZGeneratedStoneComponent>(lower), Is.False, "Nothing is dug.");
                });
            }
            finally
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
                SEntMan.DeleteEntity(lower);
            }
        });
    }

    [Test]
    public async Task CollapsiblePlacementOverUnsupportedAirIsRefused()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var maps = Server.System<SharedMapSystem>();
            var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            // Nothing below holds Z+1 up here: no beam and no wall on the ground level.
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, world, out var upper, out var upperGrid), Is.True);
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            var tile = maps.WorldToTile(upperGrid, gridComp, world);
            var before = SEntMan.Count<TransformComponent>();

            Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, 1, "CMWallMetal",
                new ZPlacementOptions { Mode = ZSupportMode.Collapsible }, out var placed), Is.False,
                "A collapsible structure must not be placed where it would collapse five seconds later.");
            Assert.Multiple(() =>
            {
                Assert.That(placed, Is.EqualTo(EntityUid.Invalid));
                Assert.That(SEntMan.Count<TransformComponent>(), Is.EqualTo(before), "No structure or marker may be left.");
                Assert.That(maps.GetTileRef(upperGrid, gridComp, tile).Tile.IsEmpty, Is.True, "No floor may be left.");
                Assert.That(maps.GetAllTiles(upperGrid, gridComp).Any(), Is.False);
            });
            SEntMan.DeleteEntity(upper);
        });
    }

    [Test]
    public async Task BlockedFootprintIsRefusedWithoutLeavingAnything()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var maps = Server.System<SharedMapSystem>();
            var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, world, out var upper, out var upperGrid), Is.True);
            var gridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
            var origin = maps.WorldToTile(upperGrid, gridComp, world);
            // One tile of the 2x2 is an entity opening: it passes the pre-floor checks, so the floor is laid on the
            // other three tiles first and must be rolled back.
            var blockedTile = origin + new Vector2i(1, 1);
            maps.SetTile(upperGrid, gridComp, blockedTile, map.Tile.Tile);
            SEntMan.SpawnEntity("CMUTestPlacementOpening", maps.GridTileToLocal(upperGrid, gridComp, blockedTile));
            var before = SEntMan.Count<TransformComponent>();

            // Support is not required here, so the refusal comes from the post-floor opening check and its rollback.
            Assert.That(building.TryPlaceStructureOnLevel(map.MapUid, world, 1, "CMUTestPlacement2x2",
                new ZPlacementOptions
                {
                    Mode = ZSupportMode.Collapsible,
                    RequireSupport = false,
                }, out var placed), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(placed, Is.EqualTo(EntityUid.Invalid));
                Assert.That(SEntMan.EntityQuery<ZFootprintComponent>().Any(), Is.False, "Nothing may be spawned.");
                Assert.That(SEntMan.Count<TransformComponent>(), Is.EqualTo(before), "No marker or structure may be left.");
                foreach (var empty in new[] { origin, origin + new Vector2i(1, 0), origin + new Vector2i(0, 1) })
                {
                    Assert.That(maps.GetTileRef(upperGrid, gridComp, empty).Tile.IsEmpty, Is.True,
                        $"The floor laid at {empty} must be rolled back.");
                }
                Assert.That(maps.GetTileRef(upperGrid, gridComp, blockedTile).Tile, Is.EqualTo(map.Tile.Tile));
            });
            SEntMan.DeleteEntity(upper);
        });
    }
}
