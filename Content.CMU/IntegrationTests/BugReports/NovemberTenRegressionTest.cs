using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.Xenonids.Weeds;
using Content.Client.IconSmoothing;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Crafting;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Xenonids.Weeds;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class NovemberTenRegressionTest : GameTest
{
    // the UNISC lead enforcer was the only team leader without orders
    [Test]
    public async Task UniscLeadEnforcerCanGiveOrders()
    {
        await Server.WaitAssertion(() =>
        {
            var job = SProtoMan.Index<JobPrototype>("AU14JobUNISCEnforcerLeader");
            Assert.That(job.RoundComponents.ContainsKey("MarineOrders"), Is.True);
        });
    }

    // every ambassador merc came out named "Nameless mercenary"
    [Test]
    public async Task AmbassadorMercenariesGetTheirOwnNames()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var first = SEntMan.SpawnEntity("RUMCJobTWEMerc", map.GridCoords);
            var second = SEntMan.SpawnEntity("RUMCJobUPPMercLeader", map.GridCoords);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(first).EntityName, Is.Not.EqualTo("Nameless mercenary"));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(second).EntityName, Is.Not.EqualTo("Nameless mercenary"));
        });
    }

    // pathogen weeds grew the xeno wall weed, their own mycelium wall weed was never used
    [Test]
    public async Task PathogenWeedsPickTheirOwnWallWeed()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords);
            var pathogen = SEntMan.SpawnEntity("CMU14XenoMyceliumWeeds", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var xeno = SEntMan.SpawnEntity("XenoWeeds", map.GridCoords.Offset(new System.Numerics.Vector2(-1, 0)));
            var weedable = SEntMan.GetComponent<XenoWeedableComponent>(wall);
            Assert.That(SEntMan.HasComponent<CMUWallWeedsSpawnComponent>(pathogen), Is.True);

            var ev = new AfterEntityWeedingEvent(pathogen, wall);
            SEntMan.EventBus.RaiseLocalEvent(pathogen, ref ev);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo("CMU14XenoMyceliumWeedsWall"));

            var back = new AfterEntityWeedingEvent(xeno, wall);
            SEntMan.EventBus.RaiseLocalEvent(xeno, ref back);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo("XenoWeedsWall"), "ordinary weeds put the xeno wall weed back");
        });
    }

    // pathogen used to force its wall weed onto anything weedable, so windows, frames and the comms tower
    // (which has no wall weed at all) would all have grown a mycelium wall
    [TestCase("CMWindowReinforcedAlmayer", "CMUXenoWeedsWindow", "CMU14XenoMyceliumWeedsWindow")]
    [TestCase("RMCWindowFrameAlmayer", "CMUXenoWeedsWindowFrame", "CMU14XenoMyceliumWeedsWindowFrame")]
    [TestCase("RMCCommunicationsTower", null, null)]
    public async Task PathogenWeedsKeepEachWeedableShape(string target, string? xenoWeed, string? pathogenWeed)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var weeded = SEntMan.SpawnEntity(target, map.GridCoords);
            var pathogen = SEntMan.SpawnEntity("CMU14XenoMyceliumWeeds", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var xeno = SEntMan.SpawnEntity("XenoWeeds", map.GridCoords.Offset(new System.Numerics.Vector2(-1, 0)));
            var weedable = SEntMan.GetComponent<XenoWeedableComponent>(weeded);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo(xenoWeed));

            var ev = new AfterEntityWeedingEvent(pathogen, weeded);
            SEntMan.EventBus.RaiseLocalEvent(pathogen, ref ev);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo(pathogenWeed));

            var back = new AfterEntityWeedingEvent(xeno, weeded);
            SEntMan.EventBus.RaiseLocalEvent(xeno, ref back);
            Assert.That(weedable.Spawn?.Id, Is.EqualTo(xenoWeed));
        });
    }

    // windows and frames never got weeded, the weedwindow/weedframe sprites in both weed rsis sat unused.
    // real spread from a node: a wall, a north-south run of three windows and an east-west pair of frames
    [TestCase("XenoWeedsSource", "XenoWeedsWall", "CMUXenoWeedsWindow", "CMUXenoWeedsWindowFrame")]
    [TestCase("CMU14XenoMyceliumWeedsSource", "CMU14XenoMyceliumWeedsWall", "CMU14XenoMyceliumWeedsWindow", "CMU14XenoMyceliumWeedsWindowFrame")]
    public async Task WeedsGrowOverWindowsAndFramesWithTheirJunction(string node, string wallWeed, string windowWeed, string frameWeed)
    {
        var map = await Pair.CreateTestMap();
        var targets = new Dictionary<Vector2i, (string Proto, string Weed, string State)>
        {
            [new Vector2i(-2, 0)] = ("CMWallMetal", wallWeed, ""),
            [new Vector2i(2, 1)] = ("CMWindowReinforcedAlmayer", windowWeed, "weedwindow2"),
            [new Vector2i(2, 0)] = ("CMWindowReinforcedAlmayer", windowWeed, "weedwindow3"),
            [new Vector2i(2, -1)] = ("CMWindowReinforcedAlmayer", windowWeed, "weedwindow1"),
            [new Vector2i(0, 2)] = ("RMCWindowFrameAlmayer", frameWeed, "weedframe4"),
            [new Vector2i(1, 2)] = ("RMCWindowFrameAlmayer", frameWeed, "weedframe8"),
        };
        var spawned = new Dictionary<Vector2i, EntityUid>();

        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var plating = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), plating);

            foreach (var (tile, target) in targets)
                spawned[tile] = SEntMan.SpawnEntity(target.Proto, maps.GridTileToLocal(map.Grid, map.Grid.Comp, tile));

            SEntMan.SpawnEntity(node, maps.GridTileToLocal(map.Grid, map.Grid.Comp, Vector2i.Zero));
        });

        // two rings of spread at 3.33s each
        for (var i = 0; i < 40; i++)
        {
            await RunTicksSync(30);
            var done = false;
            await Server.WaitPost(() => done = spawned.Values.All(uid => SEntMan.GetComponent<XenoWeedableComponent>(uid).Entity != null));
            if (done)
                break;
        }

        var overlays = new Dictionary<Vector2i, NetEntity>();
        await Server.WaitAssertion(() =>
        {
            foreach (var (tile, target) in targets)
            {
                var overlay = SEntMan.GetComponent<XenoWeedableComponent>(spawned[tile]).Entity;
                Assert.That(overlay, Is.Not.Null, $"{target.Proto} at {tile} never got weeded");
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(overlay!.Value).EntityPrototype?.ID, Is.EqualTo(target.Weed), $"{target.Proto} at {tile}");
                overlays[tile] = SEntMan.GetNetEntity(overlay.Value);
            }
        });

        await RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            CEntMan.System<IconSmoothSystem>().FrameUpdate(0);
            CEntMan.System<CMUWeededJunctionSpriteSystem>().FrameUpdate(0);
            var sprites = CEntMan.System<SpriteSystem>();
            foreach (var (tile, target) in targets)
            {
                if (target.State == "")
                    continue;

                var overlay = CEntMan.GetEntity(overlays[tile]);
                Assert.That(sprites.LayerGetRsiState((overlay, null), 0).ToString(), Is.EqualTo(target.State), $"{target.Proto} at {tile}");
            }
        });

        await Server.WaitAssertion(() =>
        {
            foreach (var (tile, overlay) in overlays)
            {
                SEntMan.DeleteEntity(SEntMan.GetEntity(overlay));
                var weedable = SEntMan.GetComponent<XenoWeedableComponent>(spawned[tile]);
                Assert.That(weedable.Entity, Is.Null, "Destroyed coverings must release both walls and windows immediately.");
                SEntMan.GetComponentState(SEntMan.EventBus, weedable, null, Robust.Shared.Timing.GameTick.Zero);
            }
        });
    }

    // corners and tees have no cmss13 weed sprite, they fall back to a straight run instead of the error sprite
    [TestCase(0, 0)]
    [TestCase(3, 3)]
    [TestCase(12, 12)]
    [TestCase(5, 4)]
    [TestCase(10, 8)]
    [TestCase(7, 3)]
    [TestCase(13, 12)]
    [TestCase(15, 12)]
    public void WeedJunctionFallsBackToAStraightRun(int junction, int expected)
    {
        var states = new List<int> { 0, 1, 2, 3, 4, 8, 12 };
        Assert.That(CMUWeededJunctionSpriteSystem.PickState(states, junction), Is.EqualTo(expected));
    }

    // sand was the one recipe behind a construction skill, the menu hid it from everyone without it
    [Test]
    public async Task SandRecipeListsWithoutConstructionSkill()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var powder = SEntMan.SpawnEntity("RUMCStonePowderItem", map.GridCoords);
            SEntMan.System<SharedStackSystem>().SetCount(powder, 4);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, powder), Is.True);

            var recipes = SEntMan.System<CraftingSystem>().GetAvailableRecipes(user).Select(r => r.ID).ToList();
            Assert.That(recipes, Does.Contain("RUMCCraftSand"));
        });
    }
}
