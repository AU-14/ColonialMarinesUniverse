using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// ARCH-03: one NxM footprint check, reporting every failing tile.
/// </summary>
[TestFixture]
[TestOf(typeof(ZFootprintSystem))]
public sealed class ZFootprintTest : GameTest
{
    private static readonly Vector2i Size2 = new(2, 2);
    private static readonly Vector2i Roofed = new(0, 0);
    private static readonly Vector2i EmptyOrigin = new(10, 0);
    private static readonly Vector2i PitOrigin = new(13, 0);
    private static readonly Vector2i WallOrigin = new(16, 0);
    private static readonly Vector2i OpenSkyOrigin = new(20, 0);
    private static readonly Vector2i BigOrigin = new(30, 0);
    // On the roof level: over the ground wall at WallOrigin, and far (> cantilever reach) from every wall over open air.
    private static readonly Vector2i OverWall = WallOrigin;
    private static readonly Vector2i OverAir = new(25, 0);

    private readonly List<EntityUid> _cleanup = new();

    [Test]
    public async Task FootprintPassesOnSolidFloorAndReportsEveryBlockedTile()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var footprint = Server.System<ZFootprintSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var plating = new Tile(tiles["Plating"].TileId);

            // Below: solid, so pits do not dig. Ground: the footprints. Roof: only over the roofed footprint.
            var levels = new Dictionary<EntityUid, int>();
            var grids = new Dictionary<int, Entity<MapGridComponent>>();
            for (var depth = -1; depth <= 1; depth++)
            {
                var map = maps.CreateMap(runMapInit: true);
                _cleanup.Add(map);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                for (var x = depth == 1 ? -1 : -2; x <= (depth == 1 ? 2 : 35); x++)
                for (var y = depth == 1 ? -1 : -2; y <= (depth == 1 ? 2 : 4); y++)
                    maps.SetTile(map, grid, new Vector2i(x, y), plating);
                levels.Add(map, depth);
                grids.Add(depth, (map, grid));
            }

            var network = z.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(z.TryAddMapsIntoZNetwork(network, levels), Is.True);

            var ground = grids[0];
            EntityUid SpawnAt(string proto, Vector2i tile)
            {
                var uid = SEntMan.SpawnEntity(proto, maps.GridTileToLocal(ground.Owner, ground.Comp, tile));
                Assert.That(SComp<TransformComponent>(uid).Anchored, Is.True, $"{proto} must be anchored.");
                return uid;
            }

            var bigEmpty = BigOrigin;
            var bigPit = BigOrigin + new Vector2i(1, 1);
            var bigWall = BigOrigin + new Vector2i(2, 2);
            maps.SetTile(ground.Owner, ground.Comp, EmptyOrigin, Tile.Empty);
            maps.SetTile(ground.Owner, ground.Comp, bigEmpty, Tile.Empty);
            SpawnAt("RUMCPit", PitOrigin);
            SpawnAt("RUMCPit", bigPit);
            SpawnAt("WallSolid", WallOrigin);
            SpawnAt("WallSolid", bigWall);

            var rules = new ZFootprintRules();
            var blocked = new HashSet<Vector2i>();
            bool Check(Vector2i origin, Vector2i size, ZFootprintRules ruleSet)
            {
                blocked.Clear();
                return footprint.ValidateFootprint(ground.Owner, ground, origin, size, ruleSet, blocked);
            }

            Assert.Multiple(() =>
            {
                Assert.That(Check(Roofed, Size2, rules), Is.True, "A 2x2 on solid floor passes.");
                Assert.That(blocked, Is.Empty);
                Assert.That(footprint.ValidateFootprint(ground.Owner, ground, Roofed, Size2, rules), Is.True,
                    "The check works without a blocked set.");

                Assert.That(Check(EmptyOrigin, Size2, rules), Is.False, "An empty tile fails.");
                Assert.That(blocked, Is.EquivalentTo(new[] { EmptyOrigin }));

                Assert.That(Check(PitOrigin, Size2, rules), Is.False, "A pit fails.");
                Assert.That(blocked, Is.EquivalentTo(new[] { PitOrigin }));

                Assert.That(Check(WallOrigin, Size2, rules), Is.False, "A blocking anchored wall fails.");
                Assert.That(blocked, Is.EquivalentTo(new[] { WallOrigin }));
                Assert.That(Check(WallOrigin, Size2, new ZFootprintRules { BlockMask = CollisionGroup.None }),
                    Is.True, "With no block mask the wall is not checked.");
            });

            var ceiling = new ZFootprintRules { RequireCeiling = CMUZCeilingMode.Roof };
            Assert.Multiple(() =>
            {
                Assert.That(Check(OpenSkyOrigin, Size2, ceiling), Is.False, "Open sky fails a ceiling requirement.");
                Assert.That(blocked, Has.Count.EqualTo(4));
                Assert.That(Check(Roofed, Size2, ceiling), Is.True, "Under a roof the ceiling requirement passes.");
            });

            Assert.That(Check(BigOrigin, new Vector2i(3, 3), rules), Is.False);
            Assert.That(blocked, Is.EquivalentTo(new[] { bigEmpty, bigPit, bigWall }),
                "A 3x3 reports every blocked tile, not only the first.");

            // Support on Z+1, isolated from the tile and blocking rules (a fall-through tile fails those on its own).
            var upper = grids[1];
            var openAir = new Tile(tiles["CMUFloorOpenAirSealed"].TileId);
            maps.SetTile(ground.Owner, ground.Comp, OverAir, Tile.Empty);
            maps.SetTile(upper.Owner, upper.Comp, OverAir, openAir);
            maps.SetTile(upper.Owner, upper.Comp, OverWall, openAir);
            var support = new ZFootprintRules
            {
                RequireSupport = true,
                RequireSolidTile = false,
                RejectOpenings = false,
                BlockMask = CollisionGroup.None,
            };
            Assert.Multiple(() =>
            {
                blocked.Clear();
                Assert.That(footprint.ValidateFootprint(upper.Owner, upper, OverAir, Vector2i.One, support, blocked), Is.False,
                    "A tile over open air on Z+1 is not supported.");
                Assert.That(blocked, Is.EquivalentTo(new[] { OverAir }));
                Assert.That(footprint.ValidateFootprint(upper.Owner, upper, OverWall, Vector2i.One, support), Is.True,
                    "A wall directly below supports the tile.");
                Assert.That(footprint.ValidateFootprint(upper.Owner, upper, OverAir, Vector2i.One,
                    new ZFootprintRules { RequireSolidTile = false, RejectOpenings = false, BlockMask = CollisionGroup.None }), Is.True,
                    "Without the support rule the same tile passes.");
                Assert.That(Check(EmptyOrigin, Vector2i.One, support), Is.True,
                    "Depth 0 never collapses, so the support rule passes there.");
            });
        });
    }

    [TearDown]
    public async Task CleanupScenario()
    {
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
        _cleanup.Clear();
    }
}
