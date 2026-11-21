using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// The D12 gate (GraphAudit TEST-02): an idle authored multi-level world must never collapse.
/// While explorers stand on every level, nothing may schedule a support collapse or a cave-in, no authored
/// level may turn into non-localized stone, and no authored tile or anchored entity may change.
/// Case B is the fast fixture for every PR. Cases A and C load real game maps and are run once per gate.
/// </summary>
[TestFixture]
public sealed class CMUStableGarrisonIdleCollapseTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const int FixtureSoakTicks = 300;
    private const float GameMapSoakSeconds = 20f;
    // Lets map-init and first-update churn (self-deleting spawners and the like) finish before the snapshot.
    // Far shorter than any collapse warning, so it cannot hide a collapse.
    private const float GameMapSettleSeconds = 1f;

    private sealed class LevelSnapshot
    {
        public readonly Dictionary<(EntityUid Grid, Vector2i Tile), int> Tiles = new();
        public readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile, string? Proto)> Anchored = new();
        /// <summary>The cells stone generation owned when the snapshot was taken.</summary>
        public readonly HashSet<(EntityUid Grid, Vector2i Tile)> Generated = new();
    }

    // Bounds the landing search on a real map's largest grid.
    private const int MaxLandingCandidates = 200_000;
    // Lets the first streaming around fresh landings finish before the snapshot it is compared against.
    private const float LandingSettleSeconds = 2f;

    // The cells at Chebyshev distance 1 and 2. A landing whose ring 1 touches mapped content is sealed cavern
    // boundary (IsCavernBoundary), so "beside mapped content" means mapped content in ring 2.
    private static readonly Vector2[] Ring1 = Ring(1);
    private static readonly Vector2[] Ring2 = Ring(2);

    private static Vector2[] Ring(int distance)
    {
        var cells = new List<Vector2>();
        for (var x = -distance; x <= distance; x++)
        for (var y = -distance; y <= distance; y++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) == distance)
                cells.Add(new Vector2(x, y));
        }

        return cells.ToArray();
    }

    /// <summary>
    /// Case B: an authored three-level fixture over a generated cave, with down-stair landings generating localized
    /// stone on authored Z1 and an explorer on every level and at every landing.
    /// </summary>
    [Test]
    public async Task IdleAuthoredFixtureOverGeneratedCaveNeverCollapses()
    {
        var map = await Pair.CreateTestMap();
        var originalZLevels = true;
        var levels = new List<EntityUid>();
        var authored = new List<EntityUid>();
        var cleanup = new List<EntityUid>();
        EntityUid cave = default;
        EntityUid upperLevel = default;
        Dictionary<EntityUid, LevelSnapshot> before = default!;
        try
        {
            await Server.WaitAssertion(() =>
            {
                originalZLevels = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled);
                Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, true);
                var building = Server.System<ZLevelBuildingSystem>();
                var maps = Server.System<SharedMapSystem>();
                var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
                settings.DepthBiomes.Clear();
                settings.StoneOreVeins.Clear();
                settings.StoneDeposits.Clear();
                settings.StoneCaveDecor.Clear();
                settings.StoneWater = null;

                // Z0: the authored ground floor with a mapped room wall.
                for (var x = 0; x < 16; x++)
                for (var y = 0; y < 16; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
                for (var x = 2; x < 7; x++)
                    SEntMan.SpawnEntity("CMWallMetal", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, 2)));
                SEntMan.EnsureComponent<ZAuthoredLevelComponent>(map.MapUid);

                // Z1: an authored upper floor. Its mapped beam stands over plain floor, with no wall below it. The
                // solver roots it through the authored-floor seed (ZLevelSupportSystem.TryGetSeedBudget: an
                // upper-level support on a solid tile with no floor marker is always rooted), and the soak proves
                // that idling never schedules such an authored structure.
                Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, Vector2.Zero, out var upper, out var upperGrid), Is.True);
                cleanup.Add(upper);
                SEntMan.EnsureComponent<ZAuthoredLevelComponent>(upper);
                upperLevel = upper;
                var upperGridComp = SEntMan.GetComponent<MapGridComponent>(upperGrid);
                for (var x = 2; x < 10; x++)
                for (var y = 2; y < 10; y++)
                {
                    var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y));
                    maps.SetTile(upperGrid, upperGridComp, maps.WorldToTile(upperGrid, upperGridComp, world), map.Tile.Tile);
                }
                var wallAbove = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(3, 2));
                SEntMan.SpawnEntity("CMWallMetal", maps.GridTileToLocal(upperGrid, upperGridComp, maps.WorldToTile(upperGrid, upperGridComp, wallAbove)));
                var beamAbove = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(7, 7));
                SEntMan.SpawnEntity("AU14NavalisSupportBeamBrown1Tile", maps.GridTileToLocal(upperGrid, upperGridComp, maps.WorldToTile(upperGrid, upperGridComp, beamAbove)));

                // Z-1: a generated (runtime, non-authored) cave level that streams in around its explorer.
                Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out cave, out var caveGrid), Is.True);
                cleanup.Add(cave);
                SEntMan.EnsureComponent<ZGeneratedStoneComponent>(cave).StoneGrid = caveGrid;
                Assert.That(SEntMan.HasComponent<ZAuthoredLevelComponent>(cave), Is.False);

                // Z2: an authored top floor over all of Z1. Down-stairs from it land on Z1, the historical bug's
                // setting: stone generated on an authored level, over a void and beside its mapped content.
                Assert.That(building.EnsureNeighborLevel(upper, 1, upperGrid, Vector2.Zero, out var top, out var topGrid), Is.True);
                cleanup.Add(top);
                SEntMan.EnsureComponent<ZAuthoredLevelComponent>(top);
                var topGridComp = SEntMan.GetComponent<MapGridComponent>(topGrid);
                for (var x = 0; x < 16; x++)
                for (var y = 0; y < 16; y++)
                {
                    var world = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y));
                    maps.SetTile(topGrid, topGridComp, maps.WorldToTile(topGrid, topGridComp, world), map.Tile.Tile);
                }
                var roomWall = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(9, 5));
                SEntMan.SpawnEntity("CMWallMetal", maps.GridTileToLocal(upperGrid, upperGridComp, maps.WorldToTile(upperGrid, upperGridComp, roomWall)));

                var upperMapId = SEntMan.GetComponent<MapComponent>(upper).MapId;
                // Over a void (nothing on Z1 within two tiles), and two tiles from Z1's room wall: one tile closer
                // is the sealed cavern boundary, where no stair may land.
                foreach (var landingTile in new[] { new Vector2i(13, 13), new Vector2i(11, 5) })
                {
                    var landing = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, landingTile);
                    Assert.That(building.PrepareStoneForStair(top, topGrid, landing, out var stoneGrid, ignorePolicy: true), Is.True,
                        $"A down-stair from Z2 must land on Z1 at {landingTile}.");
                    cleanup.Add(SpawnExplorer(stoneGrid,
                        Server.System<SharedTransformSystem>().ToCoordinates(stoneGrid, new MapCoordinates(landing, upperMapId))));
                }

                authored.AddRange(new[] { map.MapUid, upper, top });
                levels.AddRange(new[] { cave, map.MapUid, upper, top });
                cleanup.Add(SpawnExplorer(map.Grid.Owner, maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, new Vector2i(8, 8))));
                cleanup.Add(SpawnExplorer(upperGrid, maps.GridTileToLocal(upperGrid, upperGridComp,
                    maps.WorldToTile(upperGrid, upperGridComp, maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(5, 5))))));
                cleanup.Add(SpawnExplorer(topGrid, maps.GridTileToLocal(topGrid, topGridComp,
                    maps.WorldToTile(topGrid, topGridComp, maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, new Vector2i(8, 8))))));
                cleanup.Add(SpawnExplorer(caveGrid, new EntityCoordinates(caveGrid, new Vector2(0.5f, 0.5f))));
            });

            // The stairs and their first chunks are part of the baseline; only what follows is compared.
            await Server.WaitRunTicks(SecondsToTicks(LandingSettleSeconds));
            await Server.WaitAssertion(() => before = Snapshot(authored));

            await Server.WaitRunTicks(FixtureSoakTicks);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<ZGeneratedStoneComponent>(cave).GeneratedChunks, Is.Not.Empty,
                    "The cave below the authored levels must actually stream in, or the soak proves nothing.");
                AssertLandingsLocalized(new[] { upperLevel });
                AssertCollapseInvariants(levels, authored);
                AssertUnchanged(before, Snapshot(authored), "after the idle soak");
                AssertDumpChangesNothing(levels, authored);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in cleanup)
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }

                Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, originalZLevels);
            });
        }
    }

    /// <summary>Case A: the real Stable Garrison with every level, loaded through the round flow's game-map path.</summary>
    [Test]
    [Explicit("Loads every level of a full game map and idles about 20 s; run once per D12 gate.")]
    public async Task IdleStableGarrisonReduxNeverCollapses()
    {
        await SoakGameMap("StableGarrisonRedux", requireLegacySecondaryLevel: false);
    }

    /// <summary>Case C: a legacy map whose secondary levels carry no <c>BecomesStation</c>.</summary>
    [Test]
    [Explicit("Loads every level of a full game map and idles about 20 s; run once per D12 gate.")]
    public async Task IdleLegacyLv624NeverCollapses()
    {
        await SoakGameMap("CMULV624", requireLegacySecondaryLevel: true);
    }

    private async Task SoakGameMap(string gameMapId, bool requireLegacySecondaryLevel)
    {
        var originalZLevels = true;
        var levels = new List<EntityUid>();
        var explorers = new List<EntityUid>();
        EntityUid network = default;
        Dictionary<EntityUid, LevelSnapshot> before = default!;
        try
        {
            await Server.WaitAssertion(() =>
            {
                originalZLevels = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled);
                Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, true);
                var proto = SProtoMan.Index<GameMapPrototype>(gameMapId);
                // GameTicker.LoadGameMap raises PostGameMapLoad, so CMUZLevelsSystem.OnGameMapLoad loads, marks and
                // links every level exactly as at round start.
                Server.System<GameTicker>().LoadGameMap(proto, out var mapId,
                    DeserializationOptions.Default with { InitializeMaps = true });
                var main = Server.System<SharedMapSystem>().GetMap(mapId);
                var zLevels = Server.System<CMUZLevelsSystem>();
                levels.AddRange(zLevels.GetAllNetworkMaps(main));
                if (zLevels.TryGetZNetwork(main, out var zNetwork))
                    network = zNetwork.Value.Owner;

                Assert.That(levels, Has.Count.EqualTo(1 + proto.MapsAbove.Count + proto.MapsBelow.Count),
                    $"Every level of {gameMapId} must load into one z-network.");
                Assert.That(levels.All(level => SEntMan.HasComponent<ZAuthoredLevelComponent>(level)), Is.True,
                    "The game-map load must mark every level it authors.");
                if (requireLegacySecondaryLevel)
                {
                    Assert.That(levels.Any(level => level != main && !HasStationGrid(level)), Is.True,
                        $"{gameMapId} no longer has a secondary level without BecomesStation; pick another legacy map.");
                }
            });

            await Server.WaitRunTicks(SecondsToTicks(GameMapSettleSeconds));

            var landedOn = new List<EntityUid>();
            await Server.WaitAssertion(() =>
            {
                foreach (var level in levels)
                    explorers.Add(SpawnExplorerOnLevel(level));
                landedOn.AddRange(PrepareStairLandings(levels, explorers));
            });

            // The stairs and their first chunks are part of the baseline; only what follows is compared.
            await Server.WaitRunTicks(SecondsToTicks(LandingSettleSeconds));
            await Server.WaitAssertion(() => before = Snapshot(levels));

            await Server.WaitRunTicks(SecondsToTicks(GameMapSoakSeconds));

            await Server.WaitAssertion(() =>
            {
                AssertLandingsLocalized(landedOn);
                AssertCollapseInvariants(levels, levels);
                AssertUnchanged(before, Snapshot(levels), "after the idle soak");
                AssertDumpChangesNothing(levels, levels);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in explorers.Concat(levels).Append(network))
                {
                    if (uid.IsValid() && SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }

                Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, originalZLevels);
            });
        }
    }

    private int SecondsToTicks(float seconds) => (int) MathF.Ceiling(seconds * SGameTiming.TickRate);

    private ITileDefinitionManager TileDefinitions => Server.ResolveDependency<ITileDefinitionManager>();

    /// <summary>A non-ghost explorer without a session: streaming and border mirroring follow it like a player.</summary>
    private EntityUid SpawnExplorer(EntityUid grid, EntityCoordinates coordinates)
    {
        var explorer = SEntMan.SpawnEntity(null, coordinates);
        SEntMan.AddComponent<CMUZStreamAnchorComponent>(explorer);
        Assert.That(SEntMan.GetComponent<TransformComponent>(explorer).GridUid, Is.EqualTo(grid));
        return explorer;
    }

    /// <summary>Places an explorer on a floor tile in the middle of the level's largest grid.</summary>
    private EntityUid SpawnExplorerOnLevel(EntityUid level)
    {
        var maps = Server.System<SharedMapSystem>();
        EntityUid? best = null;
        var bestArea = -1f;
        foreach (var grid in GridsOn(level))
        {
            var bounds = SEntMan.GetComponent<MapGridComponent>(grid).LocalAABB;
            if (bounds.Width * bounds.Height <= bestArea)
                continue;

            best = grid;
            bestArea = bounds.Width * bounds.Height;
        }

        Assert.That(best, Is.Not.Null, $"{SToPrettyString(level)} has no grid to explore.");
        var gridComp = SEntMan.GetComponent<MapGridComponent>(best!.Value);
        var tiles = maps.GetAllTiles(best.Value, gridComp).ToList();
        Assert.That(tiles, Is.Not.Empty, $"{SToPrettyString(level)} has no floor to stand on.");
        var tile = tiles[tiles.Count / 2].GridIndices;
        return SpawnExplorer(best.Value, maps.GridTileToLocal(best.Value, gridComp, tile));
    }

    private List<EntityUid> GridsOn(EntityUid level)
    {
        var grids = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == level)
                grids.Add(uid);
        }

        return grids;
    }

    private bool HasStationGrid(EntityUid level)
        => SEntMan.HasComponent<BecomesStationComponent>(level) ||
           GridsOn(level).Any(grid => SEntMan.HasComponent<BecomesStationComponent>(grid));

    /// <summary>Every tile and every anchored entity of the given levels.</summary>
    private Dictionary<EntityUid, LevelSnapshot> Snapshot(IEnumerable<EntityUid> levels)
    {
        var maps = Server.System<SharedMapSystem>();
        var snapshots = new Dictionary<EntityUid, LevelSnapshot>();
        foreach (var level in levels)
        {
            var snapshot = new LevelSnapshot();
            foreach (var grid in GridsOn(level))
            {
                foreach (var tile in maps.GetAllTiles(grid, SEntMan.GetComponent<MapGridComponent>(grid)))
                    snapshot.Tiles[(grid, tile.GridIndices)] = tile.Tile.TypeId;
            }

            if (SEntMan.TryGetComponent<ZGeneratedStoneComponent>(level, out var stone))
            {
                foreach (var tile in stone.GeneratedTiles)
                    snapshot.Generated.Add((stone.StoneGrid, tile));
            }

            snapshots[level] = snapshot;
        }

        var anchored = SEntMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (anchored.MoveNext(out var uid, out var xform, out var meta))
        {
            if (!xform.Anchored || xform.MapUid is not { } map || xform.GridUid is not { } grid ||
                !snapshots.TryGetValue(map, out var snapshot))
                continue;

            var tile = maps.TileIndicesFor(grid, SEntMan.GetComponent<MapGridComponent>(grid), xform.Coordinates);
            snapshot.Anchored[uid] = (grid, tile, meta.EntityPrototype?.ID);
        }

        return snapshots;
    }

    /// <summary>
    /// Asserts that no authored content of the levels changed between the baseline and now. The authored cells
    /// are the baseline's tiles minus those stone generation already owned at baseline (a localized cave's
    /// <see cref="ZGeneratedStoneComponent.GeneratedTiles"/>, copied into the baseline). Cells absent from the
    /// baseline are new cave that streaming keeps adding around a landing, which is generation, not collapse.
    /// Ownership is read from the baseline only: an authored tile that a collapse empties and that a later chunk
    /// then fills stays authored, so the loss is still caught. Anchored entities follow the cell they stand on.
    /// </summary>
    private void AssertUnchanged(Dictionary<EntityUid, LevelSnapshot> before, Dictionary<EntityUid, LevelSnapshot> after, string when)
    {
        Assert.Multiple(() =>
        {
            foreach (var (level, old) in before)
            {
                var now = after[level];
                var name = SToPrettyString(level);
                bool Authored((EntityUid Grid, Vector2i Tile) cell)
                    => old.Tiles.ContainsKey(cell) && !old.Generated.Contains(cell);

                var changedTiles = old.Tiles
                    .Where(pair => Authored(pair.Key) &&
                                   (!now.Tiles.TryGetValue(pair.Key, out var type) || type != pair.Value))
                    .Select(pair => pair.Key.Tile)
                    .Take(10)
                    .ToList();
                var lost = old.Anchored
                    .Where(pair => Authored((pair.Value.Grid, pair.Value.Tile)) && !now.Anchored.ContainsKey(pair.Key))
                    .Select(pair => $"{pair.Value.Proto ?? "<no prototype>"} at {pair.Value.Tile}")
                    .Take(10)
                    .ToList();
                // Collapse symptoms only: debris that fell from another level, or generated rock (a burial) on an
                // authored cell. Mapped spreaders (xeno weeds on LV624, kudzu) legitimately grow onto authored cells
                // during an idle round, so other additions are not counted.
                var generatedIds = Server.System<ZLevelBuildingSystem>().GetGeneratedEntityIds(level);
                var added = now.Anchored
                    .Where(pair => Authored((pair.Value.Grid, pair.Value.Tile)) && !old.Anchored.ContainsKey(pair.Key) &&
                                   (SEntMan.HasComponent<ZFallenDebrisComponent>(pair.Key) ||
                                    pair.Value.Proto is { } proto && generatedIds.Contains(proto)))
                    .Select(pair => $"{pair.Value.Proto ?? "<no prototype>"} at {pair.Value.Tile}")
                    .Take(10)
                    .ToList();
                Assert.That(changedTiles, Is.Empty, $"{when}: authored tiles of {name} were removed or replaced.");
                Assert.That(lost, Is.Empty, $"{when}: anchored entities of {name} were deleted or unanchored.");
                Assert.That(added, Is.Empty, $"{when}: entities were anchored onto authored tiles of {name}.");
            }
        });
    }

    /// <summary>
    /// The historical bug needs stone on an authored level. From every level with an authored level below it,
    /// prepares a down-stair landing (admin path, ignoring the dig policy) over a void of the level below, and one
    /// beside its mapped content, and puts an explorer at each landing. Returns the levels that got a landing.
    /// </summary>
    private List<EntityUid> PrepareStairLandings(IReadOnlyCollection<EntityUid> levels, List<EntityUid> explorers)
    {
        var maps = Server.System<SharedMapSystem>();
        var building = Server.System<ZLevelBuildingSystem>();
        var landedOn = new List<EntityUid>();
        foreach (var level in levels)
        {
            if (!SEntMan.TryGetComponent<CMUZLevelMapComponent>(level, out var zMap) || zMap.MapBelow is not { } below ||
                !levels.Contains(below) || LargestGrid(level) is not { } source)
                continue;

            var sourceComp = SEntMan.GetComponent<MapGridComponent>(source);
            var belowMapId = SEntMan.GetComponent<MapComponent>(below).MapId;
            var belowGrids = GridsOn(below)
                .Select(grid => new Entity<MapGridComponent>(grid, SEntMan.GetComponent<MapGridComponent>(grid)))
                .ToList();

            // Mapped or void is read from the tile data of the level below at that position, not from grid
            // presence: authored levels are map-grids, so a grid is found everywhere. Void = no tile there, an empty
            // tile, or a fall-through tile.
            bool IsMappedBelow(Vector2 pos)
            {
                foreach (var grid in belowGrids)
                {
                    var indices = maps.WorldToTile(grid.Owner, grid.Comp, pos);
                    if (maps.TryGetTileRef(grid.Owner, grid.Comp, indices, out var tile) &&
                        !CMUZLevelOpeningCache.IsFallThroughTile(tile.Tile, TileDefinitions))
                        return true;
                }

                return false;
            }

            bool IsFallThroughBelow(Vector2 pos)
            {
                foreach (var grid in belowGrids)
                {
                    var indices = maps.WorldToTile(grid.Owner, grid.Comp, pos);
                    if (maps.TryGetTileRef(grid.Owner, grid.Comp, indices, out var tile) && !tile.Tile.IsEmpty &&
                        CMUZLevelOpeningCache.IsFallThroughTile(tile.Tile, TileDefinitions))
                        return true;
                }

                return false;
            }

            // The localized cave footprint is the source floor over the void below, and a landing's 3x3 must lie in
            // it (IsCavernBoundary), so the source floor must cover the 3x3 too.
            bool SourceCovers3X3(Vector2i tile)
            {
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    if (!maps.TryGetTileRef(source, sourceComp, tile + new Vector2i(x, y), out var neighbour) ||
                        neighbour.Tile.IsEmpty)
                        return false;
                }

                return true;
            }

            var stats = new Dictionary<string, int>();
            void Count(string what) => stats[what] = stats.TryGetValue(what, out var count) ? count + 1 : 1;

            if (!building.IsEnabledOn(level) || !building.IsEnabledOn(below))
            {
                TestContext.Out.WriteLine($"{SToPrettyString(level)} -> {SToPrettyString(below)}: skipped, z-building " +
                                          $"disabled (source {building.IsEnabledOn(level)}, below {building.IsEnabledOn(below)}).");
                continue;
            }

            var doneVoid = false;
            var doneBeside = false;
            var examined = 0;
            foreach (var tileRef in maps.GetAllTiles(source, sourceComp))
            {
                if (doneVoid && doneBeside || examined++ >= MaxLandingCandidates)
                    break;

                var pos = maps.GridTileToWorldPos(source, sourceComp, tileRef.GridIndices);
                if (IsMappedBelow(pos))
                {
                    Count("mapped below");
                    continue;
                }

                if (IsFallThroughBelow(pos))
                    Count("fall-through below (treated as void)");

                if (Ring1.Any(offset => IsMappedBelow(pos + offset)))
                {
                    Count("mapped within 1 below (cavern boundary)");
                    continue;
                }

                if (!SourceCovers3X3(tileRef.GridIndices))
                {
                    Count("source floor does not cover 3x3 (cavern boundary)");
                    continue;
                }

                // Beside: mapped content two tiles away. A true void: nothing mapped within two tiles.
                var beside = Ring2.Any(offset => IsMappedBelow(pos + offset));
                Count(beside ? "candidate beside mapped" : "candidate void");
                if (beside ? doneBeside : doneVoid)
                    continue;

                if (building.IsSealedCavernTile(below, pos))
                {
                    Count("refused: sealed cavern tile");
                    continue;
                }

                if (!building.PrepareStoneForStair(level, source, pos, out var stoneGrid, ignorePolicy: true))
                {
                    Count(SEntMan.TryGetComponent<ZGeneratedStoneComponent>(below, out var refusedStone) &&
                          refusedStone.CavernFootprint != null
                        ? "refused: after conversion (cavern boundary or no grid)"
                        : "refused: before conversion");
                    continue;
                }

                if (!SEntMan.TryGetComponent<ZGeneratedStoneComponent>(below, out var stone) || stone.StoneGrid != stoneGrid ||
                    !stone.GeneratedTiles.Contains(maps.TileIndicesFor(stoneGrid, SEntMan.GetComponent<MapGridComponent>(stoneGrid),
                        new MapCoordinates(pos, belowMapId))))
                {
                    // The authored-floor branch: PrepareStoneForStair stepped onto a floor below and made no stone.
                    Count("accepted without stone (authored-floor branch)");
                    continue;
                }

                Count(beside ? "landed beside mapped" : "landed void");
                var landing = Server.System<SharedTransformSystem>().ToCoordinates(stoneGrid, new MapCoordinates(pos, belowMapId));
                explorers.Add(SpawnExplorer(stoneGrid, landing));
                if (beside)
                    doneBeside = true;
                else
                    doneVoid = true;
                if (!landedOn.Contains(below))
                    landedOn.Add(below);
            }

            TestContext.Out.WriteLine($"{SToPrettyString(level)} -> {SToPrettyString(below)}: examined={examined} " +
                                      $"void landing={doneVoid}, beside-mapped landing={doneBeside}; " +
                                      string.Join(", ", stats.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        }

        var without = levels.Where(level => SEntMan.TryGetComponent<CMUZLevelMapComponent>(level, out var zMap) &&
                                            zMap.MapBelow is { } below && levels.Contains(below) && !landedOn.Contains(below))
            .Select(level => SToPrettyString(level))
            .ToList();
        TestContext.Out.WriteLine($"Levels whose level below got no landing: {(without.Count == 0 ? "none" : string.Join(", ", without))}");

        Assert.That(landedOn, Is.Not.Empty,
            "No down-stair could be prepared onto an authored level, so the soak would never create stone there.");
        return landedOn;
    }

    /// <summary>Every authored level that received a landing holds localized stone that actually streamed in.</summary>
    private void AssertLandingsLocalized(IEnumerable<EntityUid> landedOn)
    {
        Assert.Multiple(() =>
        {
            foreach (var level in landedOn)
            {
                var name = SToPrettyString(level);
                if (!SEntMan.TryGetComponent<ZGeneratedStoneComponent>(level, out var stone))
                {
                    Assert.Fail($"{name} received a landing but has no stone.");
                    continue;
                }

                Assert.That(stone.LocalizedToAuthoredLevel, Is.True, $"Stone on authored level {name} is not localized.");
                Assert.That(stone.GeneratedChunks, Is.Not.Empty, $"Stone on authored level {name} never generated.");
            }
        });
    }

    private EntityUid? LargestGrid(EntityUid level)
    {
        EntityUid? best = null;
        var bestArea = -1f;
        foreach (var grid in GridsOn(level))
        {
            var bounds = SEntMan.GetComponent<MapGridComponent>(grid).LocalAABB;
            if (bounds.Width * bounds.Height <= bestArea)
                continue;

            best = grid;
            bestArea = bounds.Width * bounds.Height;
        }

        return best;
    }

    private void AssertCollapseInvariants(IReadOnlyCollection<EntityUid> levels, IReadOnlyCollection<EntityUid> authored)
    {
        var support = Server.System<ZLevelSupportSystem>();
        Assert.Multiple(() =>
        {
            foreach (var level in authored)
            {
                if (SEntMan.TryGetComponent<ZGeneratedStoneComponent>(level, out var stone))
                {
                    Assert.That(stone.LocalizedToAuthoredLevel, Is.True,
                        $"Authored level {SToPrettyString(level)} carries non-localized stone.");
                }
            }

            foreach (var level in levels)
            {
                var name = SToPrettyString(level);
                foreach (var grid in GridsOn(level))
                {
                    Assert.That(support.PendingCollapseCount(grid), Is.Zero,
                        $"Grid {SToPrettyString(grid)} on {name} is counting down to a support collapse.");
                }

                if (!SEntMan.TryGetComponent<ZGeneratedStoneComponent>(level, out var cave))
                    continue;

                Assert.That(cave.PendingCollapse, Is.Empty, $"{name} has pending cave-ins.");
                Assert.That(cave.DirtyTiles, Is.Empty, $"{name} has unevaluated cave tiles.");
                Assert.That(cave.CollapseQueue.Count - cave.CollapseQueueIndex, Is.LessThanOrEqualTo(0),
                    $"{name} is mid-collapse.");
            }
        });
    }

    /// <summary><c>au_zsupport dump</c> is read-only: it must leave every level, and the solver, exactly as it was.</summary>
    private void AssertDumpChangesNothing(IReadOnlyCollection<EntityUid> levels, IReadOnlyCollection<EntityUid> authored)
    {
        var support = Server.System<ZLevelSupportSystem>();
        var allGrids = levels.SelectMany(level => GridsOn(level)).ToList();
        var pendingBefore = allGrids.Sum(grid => support.PendingCollapseCount(grid));
        var cavesBefore = CaveState(levels);
        var before = Snapshot(authored);

        Server.ResolveDependency<IConsoleHost>().ExecuteCommand("au_zsupport dump");

        Assert.Multiple(() =>
        {
            Assert.That(allGrids.Sum(grid => support.PendingCollapseCount(grid)), Is.EqualTo(pendingBefore),
                "The dump must not schedule support collapses.");
            Assert.That(CaveState(levels), Is.EqualTo(cavesBefore), "The dump must not change cave state.");
        });
        AssertUnchanged(before, Snapshot(authored), "after au_zsupport dump");
    }

    private List<(EntityUid Level, int Chunks, int Tiles, int Dirty, int Pending, int Queue)> CaveState(IEnumerable<EntityUid> levels)
    {
        var state = new List<(EntityUid, int, int, int, int, int)>();
        foreach (var level in levels)
        {
            if (SEntMan.TryGetComponent<ZGeneratedStoneComponent>(level, out var stone))
            {
                state.Add((level, stone.GeneratedChunks.Count, stone.GeneratedTiles.Count, stone.DirtyTiles.Count,
                    stone.PendingCollapse.Count, stone.CollapseQueue.Count));
            }
        }

        return state;
    }
}
