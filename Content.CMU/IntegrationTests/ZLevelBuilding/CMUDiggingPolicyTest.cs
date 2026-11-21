using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevelBuilding.Pit;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevelBuilding;

[TestFixture]
public sealed class CMUDiggingPolicyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DiggingCVarRechecksToolsAndPendingExcavations()
    {
        var map = await Pair.CreateTestMap();
        EntityUid miner = default;
        EntityUid tool = default;
        EntityCoordinates location = default;
        await Server.WaitAssertion(() =>
        {
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.DepthBiomes.Clear();
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 16; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            location = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, new Vector2i(6, 5));
            miner = SEntMan.SpawnEntity("MobHuman", location);
            tool = SEntMan.SpawnEntity("Pickaxe", location);
            SEntMan.GetComponent<PitDiggingToolComponent>(tool).DigTimeMultiplier = 0.01f;
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(miner, tool), Is.True);
            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
            var digging = SEntMan.System<PitDiggingToolSystem>();
            Assert.That(digging.TryStartDig(tool, miner, location), Is.False,
                "Disabling the CVar must block downward excavation before a tool action starts.");
            Assert.That(SEntMan.System<ZLevelBuildingSystem>().DigDown(miner), Is.False);

            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
            Assert.That(digging.TryStartDig(tool, miner, location), Is.True,
                "Enabling the CVar must allow the real tool interaction on the same terrain.");
            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
        });

        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<PitComponent>().Any(), Is.False,
                "An excavation started while allowed must not open a pit after the CVar is disabled.");
            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
            Assert.That(SEntMan.System<PitDiggingToolSystem>().TryStartDig(tool, miner, location), Is.True,
                "Re-enabling the CVar must allow a new tool action on the same terrain.");
            SEntMan.DeleteEntity(miner);
            if (SEntMan.EntityExists(tool))
                SEntMan.DeleteEntity(tool);
        });
    }

    [Test]
    public async Task DigDownPolicyGatesNewLevelsButNotExistingShafts()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = SEntMan.System<ZLevelBuildingSystem>();
            var maps = SEntMan.System<SharedMapSystem>();
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.DepthBiomes.Clear();
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 16; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var tile = new Vector2i(6, 5);
            var position = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, tile);
            var location = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, tile);
            var condition = new ZDigDownAllowed();
            var mapCount = SEntMan.Count<MapComponent>();
            EntityUid lower = default;
            try
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
                Assert.Multiple(() =>
                {
                    Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, position, out _, out _), Is.False,
                        "A new level below is a new excavation.");
                    Assert.That(building.PrepareStoneForStair(map.MapUid, map.Grid.Owner, position, out _), Is.False,
                        "A down-stair must not create stone below while digging down is disallowed.");
                    Assert.That(SEntMan.Count<MapComponent>(), Is.EqualTo(mapCount), "No map may be created.");
                    Assert.That(condition.Condition(EntityUid.Invalid, location, Direction.South), Is.False,
                        "The down-stair recipe must refuse before materials are spent.");
                });

                // Explicit admin creation ignores the policy; the void level it creates is not stone yet.
                Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, position, out lower, out _,
                    ignorePolicy: true), Is.True);
                Assert.That(building.PrepareStoneForStair(map.MapUid, map.Grid.Owner, position, out _, singleTile: true), Is.False,
                    "Turning an existing level below into stone for the first time is a new excavation.");
                Assert.That(SEntMan.HasComponent<ZGeneratedStoneComponent>(lower), Is.False);
                Assert.That(condition.Condition(EntityUid.Invalid, location, Direction.South), Is.False);

                Assert.That(building.PrepareStoneForStair(map.MapUid, map.Grid.Owner, position, out var stoneGrid, singleTile: true,
                    ignorePolicy: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(building.PrepareStoneForStair(map.MapUid, map.Grid.Owner, position, out var shaftGrid, singleTile: true), Is.True,
                        "An existing stone shaft (pit expand, ladders, ropes) must keep working.");
                    Assert.That(shaftGrid, Is.EqualTo(stoneGrid));
                    Assert.That(condition.Condition(EntityUid.Invalid, location, Direction.South), Is.True,
                        "A down-stair into an existing stone level stays allowed.");
                });
            }
            finally
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
                if (SEntMan.EntityExists(lower))
                    SEntMan.DeleteEntity(lower);
            }
        });
    }

    [Test]
    public async Task DigDownPolicyAllowsDownStairOntoAuthoredFloorBelow()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = SEntMan.System<ZLevelBuildingSystem>();
            var maps = SEntMan.System<SharedMapSystem>();
            var tile = new Vector2i(6, 5);
            maps.SetTile(map.Grid, tile, map.Tile.Tile);
            var position = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, tile);
            var location = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, tile);
            // An authored level below with a walkable floor under the stair, e.g. Stable Garrison Z-1.
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, position, out var lower, out var lowerGrid), Is.True);
            SEntMan.EnsureComponent<ZAuthoredLevelComponent>(lower);
            var lowerGridComp = SEntMan.GetComponent<MapGridComponent>(lowerGrid);
            maps.SetTile(lowerGrid, lowerGridComp, maps.WorldToTile(lowerGrid, lowerGridComp, position), map.Tile.Tile);
            try
            {
                Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
                Assert.Multiple(() =>
                {
                    Assert.That(new ZDigDownAllowed().Condition(EntityUid.Invalid, location, Direction.South), Is.True,
                        "A down-stair onto an authored floor digs nothing, so the policy must not refuse it.");
                    Assert.That(building.PrepareStoneForStair(map.MapUid, map.Grid.Owner, position, out var floorGrid), Is.True);
                    Assert.That(floorGrid, Is.EqualTo(lowerGrid), "The stair must land on the authored floor's grid.");
                    Assert.That(SEntMan.HasComponent<ZGeneratedStoneComponent>(lower), Is.False,
                        "Stepping onto an authored floor must not convert the level into stone.");
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
    public async Task ZBuildDenialCoversTheNetworkAndLevelsAttachedLater()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = SEntMan.System<ZLevelBuildingSystem>();
            var toggle = SEntMan.System<ZLevelToggleSystem>();
            var zLevels = SEntMan.System<CMUZLevelsSystem>();
            var maps = SEntMan.System<SharedMapSystem>();
            var allowed = new ZBuildAllowed();
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, Vector2.Zero, out var upper, out var upperGrid), Is.True);
            EntityUid top = default;
            try
            {
                toggle.SetNetworkEnabled(map.MapUid, false);
                Assert.Multiple(() =>
                {
                    Assert.That(allowed.Condition(EntityUid.Invalid, new EntityCoordinates(upperGrid, Vector2.Zero), Direction.South), Is.False,
                        "A denial must reach Z+1, not only the main map.");
                    Assert.That(building.IsEnabledOn(upper), Is.False);
                });

                // A runtime level attached after the denial (EnsureNeighborLevel itself refuses on a denied level).
                top = maps.CreateMap(out _);
                SEntMan.EnsureComponent<MapGridComponent>(top);
                Assert.That(zLevels.TryGetZNetwork(map.MapUid, out var network), Is.True);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network!.Value, new Dictionary<EntityUid, int> { [top] = 2 }), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(allowed.Condition(EntityUid.Invalid, new EntityCoordinates(top, Vector2.Zero), Direction.South), Is.False,
                        "A level attached to a denied network later must be denied too.");
                    Assert.That(building.IsEnabledOn(top), Is.False);
                });

                // A level that leaves the denied network is no longer covered by it, and an allow would never reach it.
                Assert.That(zLevels.TryRemoveMapFromZNetwork(top), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(building.IsEnabledOn(top), Is.True, "A level that left a denied network must not stay denied.");
                    Assert.That(SEntMan.HasComponent<ZBuildableMapComponent>(top), Is.False,
                        "Settings added only for the denial must go with it.");
                    Assert.That(building.IsEnabledOn(upper), Is.False, "The rest of the network stays denied.");
                });

                toggle.SetNetworkEnabled(map.MapUid, true);
                Assert.Multiple(() =>
                {
                    Assert.That(building.IsEnabledOn(upper), Is.True);
                    Assert.That(building.IsEnabledOn(top), Is.True);
                    Assert.That(SEntMan.HasComponent<ZBuildableMapComponent>(upper), Is.False,
                        "Settings added only for the denial must not shadow the source map's settings afterwards.");
                });
            }
            finally
            {
                toggle.SetNetworkEnabled(map.MapUid, true);
                if (SEntMan.EntityExists(top))
                    SEntMan.DeleteEntity(top);
                SEntMan.DeleteEntity(upper);
            }
        });
    }

    // CMU14: the sanity cap on building up and digging down. No runtime path (admin included) creates a level outside
    // cmu.zlevels.build_min_depth..build_max_depth; levels that already exist stay reachable.
    [Test]
    public async Task DepthCapStopsNewLevelsAboveAndBelowButNotExistingOnes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = SEntMan.System<ZLevelBuildingSystem>();
            var maps = SEntMan.System<SharedMapSystem>();
            var position = maps.GridTileToWorldPos(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            var condition = new ZDigDownAllowed();
            EntityUid upper = default, lower = default;
            var maxDepth = Server.CfgMan.GetCVar(CMUZLevelsCVars.BuildMaxDepth);
            var minDepth = Server.CfgMan.GetCVar(CMUZLevelsCVars.BuildMinDepth);
            try
            {
                Server.CfgMan.SetCVar(CMUZLevelsCVars.BuildMaxDepth, 1);
                Server.CfgMan.SetCVar(CMUZLevelsCVars.BuildMinDepth, -1);

                Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, position, out upper, out _), Is.True,
                    "Depth 1 is within the cap.");
                Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, position, out lower, out _,
                    ignorePolicy: true), Is.True, "Depth -1 is within the cap.");

                var mapCount = SEntMan.Count<MapComponent>();
                Assert.Multiple(() =>
                {
                    Assert.That(building.EnsureNeighborLevel(upper, 1, upper, position, out _, out _), Is.False,
                        "No new level above the max depth.");
                    Assert.That(building.EnsureNeighborLevel(lower, -1, lower, position, out _, out _, ignorePolicy: true), Is.False,
                        "No new level below the min depth, even for admin callers.");
                    Assert.That(building.PrepareStoneForStair(lower, lower, position, out _, singleTile: true, ignorePolicy: true),
                        Is.False, "A down-stair must not create stone below the min depth.");
                    Assert.That(condition.Condition(EntityUid.Invalid, new EntityCoordinates(lower, position), Direction.South),
                        Is.False, "The down-stair recipe refuses at the cap, although digging down is allowed.");
                    Assert.That(SEntMan.Count<MapComponent>(), Is.EqualTo(mapCount), "No map may be created past the cap.");

                    // Existing levels are never affected by the cap.
                    Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, position, out var again, out _), Is.True);
                    Assert.That(again, Is.EqualTo(upper));
                });
            }
            finally
            {
                Server.CfgMan.SetCVar(CMUZLevelsCVars.BuildMaxDepth, maxDepth);
                Server.CfgMan.SetCVar(CMUZLevelsCVars.BuildMinDepth, minDepth);
                if (SEntMan.EntityExists(upper))
                    SEntMan.DeleteEntity(upper);
                if (SEntMan.EntityExists(lower))
                    SEntMan.DeleteEntity(lower);
            }
        });
    }
}
