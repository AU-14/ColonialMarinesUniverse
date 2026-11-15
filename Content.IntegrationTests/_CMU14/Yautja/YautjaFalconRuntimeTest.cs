using Content.Shared.Ghost.Components;
using Content.Shared.Speech;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Popups;
using Content.Client.StatusIcon;
using Content.Client.Viewport;
using Content.Client.CMU14.Yautja;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Examine;
using Content.Server.Speech;
using Content.Shared.Speech.Components;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Ghost;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaFalconRuntimeTest
{

    [Test]
    public async Task FalconDroneDoesNotGrantFreeZLevelActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<SharedActionsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = SpawnControllingHunter(
                entMan,
                inventory,
                map.GridCoords,
                _ => { },
                out var bracer,
                out var falcon,
                out var drone);

            try
            {
                AssertActionAbsent(entMan, actions, hunter, "CMUActionZLevelUp",
                    "Falcon control must not grant unrestricted observer Z movement.");
                AssertActionAbsent(entMan, actions, hunter, "CMUActionZLevelDown",
                    "Falcon control must not grant unrestricted observer Z movement.");

                var up = new CMUZLevelActionUp();
                entMan.EventBus.RaiseLocalEvent(hunter, up);
                Assert.That(up.Handled, Is.False);

                var down = new CMUZLevelActionDown();
                entMan.EventBus.RaiseLocalEvent(hunter, down);
                Assert.That(down.Handled, Is.False);
                Assert.That(entMan.GetComponent<TransformComponent>(drone).MapUid, Is.EqualTo(map.MapUid));
            }
            finally
            {
                DeleteAll(entMan, hunter, bracer, falcon, drone);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneUsesMappedStairs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var lowerMap = await pair.CreateTestMap();
        var upperMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();

            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
            {
                [lowerMap.MapUid] = 0,
                [upperMap.MapUid] = 1,
            }), Is.True);

            var stairs = entMan.SpawnEntity(null, lowerMap.GridCoords);
            var stairsComp = entMan.EnsureComponent<CMUZLevelStairsComponent>(stairs);
            stairsComp.Direction = Direction.East;
            stairsComp.Offset = 1;
            Assert.That(transform.AnchorEntity(stairs), Is.True);

            var hunter = SpawnControllingHunter(
                entMan,
                inventory,
                lowerMap.GridCoords,
                _ => { },
                out var bracer,
                out var falcon,
                out var drone);

            try
            {
                transform.SetCoordinates(drone, lowerMap.GridCoords.Offset(Vector2.UnitX));
                Assert.That(entMan.GetComponent<TransformComponent>(drone).MapUid, Is.EqualTo(upperMap.MapUid));
            }
            finally
            {
                DeleteAll(entMan, hunter, bracer, falcon, drone, stairs, network.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneAutomaticallyUsesMappedLadder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var lowerMap = await pair.CreateTestMap();
        var upperMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();

            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
            {
                [lowerMap.MapUid] = 0,
                [upperMap.MapUid] = 1,
            }), Is.True);

            mapSystem.SetTile(lowerMap.Grid.Owner, lowerMap.Grid.Comp, new Vector2i(1, 0), lowerMap.Tile.Tile);
            // ladders only climb onto a floor or ladder top
            mapSystem.SetTile(upperMap.Grid.Owner, upperMap.Grid.Comp, new Vector2i(1, 0), upperMap.Tile.Tile);
            var ladderCoordinates = lowerMap.GridCoords.Offset(Vector2.UnitX);
            // The ladder must have a supported landing on the destination level.
            mapSystem.SetTile(upperMap.Grid.Owner, upperMap.Grid.Comp, new Vector2i(1, 0), upperMap.Tile.Tile);
            var ladder = entMan.SpawnEntity(null, ladderCoordinates);
            var ladderComp = entMan.EnsureComponent<CMUZLevelLadderComponent>(ladder);
            ladderComp.Offset = 1;
            Assert.That(transform.AnchorEntity(ladder), Is.True);

            var hunter = SpawnControllingHunter(
                entMan,
                inventory,
                lowerMap.GridCoords,
                _ => { },
                out var bracer,
                out var falcon,
                out var drone);

            try
            {
                transform.SetCoordinates(drone, ladderCoordinates);
                Assert.That(entMan.GetComponent<TransformComponent>(drone).MapUid, Is.EqualTo(upperMap.MapUid));
            }
            finally
            {
                DeleteAll(entMan, hunter, bracer, falcon, drone, ladder, network.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneEmpConvertsDeployedHologramToDisabledWreckageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var emp = entMan.System<EmpSystem>();
                var inventory = entMan.System<InventorySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnControllingHunter(
                    entMan,
                    inventory,
                    map.GridCoords,
                    uid => server.PlayerMan.SetAttachedEntity(session, uid),
                    out bracer,
                    out falcon,
                    out drone);

                emp.DoEmpEffects(drone, 1000, TimeSpan.FromSeconds(30));
            });

            await pair.RunTicksSync(3);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.IsQueuedForDeletion(drone) || entMan.Deleted(drone), Is.True);
                    Assert.That(entMan.IsQueuedForDeletion(falcon) || entMan.Deleted(falcon), Is.True,
                        "CMSS13 falcon emp_act() qdels parent_drone instead of returning it to the controller.");
                    Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.False);
                    Assert.That(entMan.HasComponent<RelayInputMoverComponent>(hunter), Is.False);
                    Assert.That(entMan.GetComponent<EyeComponent>(hunter).Target, Is.EqualTo(hunter));
                    Assert.That(CountPrototype(entMan, "CMUYautjaFalconDroneDisabled"), Is.EqualTo(1),
                        "CMSS13 falcon emp_act() leaves disabled falcon drone wreckage.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, bracer, falcon, drone);
                DeleteByPrototype(entMan, "CMUYautjaFalconDroneDisabled");
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneDestructionConvertsDeployedHologramToDestroyedWreckageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var destructible = entMan.System<DestructibleSystem>();
                var inventory = entMan.System<InventorySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnControllingHunter(
                    entMan,
                    inventory,
                    map.GridCoords,
                    uid => server.PlayerMan.SetAttachedEntity(session, uid),
                    out bracer,
                    out falcon,
                    out drone);

                Assert.That(destructible.DestroyEntity(drone), Is.True);
            });

            await pair.RunTicksSync(3);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.IsQueuedForDeletion(drone) || entMan.Deleted(drone), Is.True);
                    Assert.That(entMan.IsQueuedForDeletion(falcon) || entMan.Deleted(falcon), Is.True,
                        "CMSS13 falcon ex_act() qdels parent_drone instead of returning it to the controller.");
                    Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.False);
                    Assert.That(entMan.HasComponent<RelayInputMoverComponent>(hunter), Is.False);
                    Assert.That(entMan.GetComponent<EyeComponent>(hunter).Target, Is.EqualTo(hunter));
                    Assert.That(CountPrototype(entMan, "CMUYautjaFalconDroneDestroyed"), Is.EqualTo(1),
                        "CMSS13 falcon ex_act() leaves destroyed falcon drone wreckage.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, bracer, falcon, drone);
                DeleteByPrototype(entMan, "CMUYautjaFalconDroneDestroyed");
            });
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnControllingHunter(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityCoordinates coordinates,
        Action<EntityUid> attachController,
        out EntityUid bracer,
        out EntityUid falcon,
        out EntityUid drone)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
        bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);
        falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", coordinates);

        entMan.EnsureComponent<YautjaComponent>(hunter);
        attachController(hunter);
        Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

        entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));
        drone = entMan.GetComponent<YautjaFalconControllerComponent>(hunter).Drone;
        Assert.That(entMan.HasComponent<YautjaFalconDroneDeployedComponent>(drone), Is.True);

        return hunter;
    }

    private static int CountPrototype(IEntityManager entMan, string prototype)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }

    private static void AssertActionAbsent(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid holder,
        string prototype,
        string message)
    {
        Assert.That(ActionPrototypeIds(entMan, actions, holder), Does.Not.Contain(prototype), message);
    }

    private static string[] ActionPrototypeIds(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid holder)
    {
        return actions.GetActions(holder)
            .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
            .ToArray();
    }

    private static void DeleteByPrototype(IEntityManager entMan, string prototype)
    {
        var toDelete = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                toDelete.Add(uid);
        }

        foreach (var uid in toDelete)
        {
            if (!entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }

    private static void DeleteAll(IEntityManager entMan, params EntityUid[] entities)
    {
        foreach (var uid in entities)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
