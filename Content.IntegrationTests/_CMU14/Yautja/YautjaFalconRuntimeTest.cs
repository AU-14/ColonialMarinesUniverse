using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Popups;
using Content.Client.StatusIcon;
using Content.Client.Viewport;
using Content.Client._CMU14.Yautja;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Examine;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Ghost;
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

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaFalconRuntimeTest
{
    private const string DeployedFalconPrototypeId = "CMUYautjaFalconDroneDeployed";
    private const string FalconHudIconPrototypeId = "CMUYautjaIconFalconDrone";
    private const string BadBloodFalconHudIconPrototypeId = "CMUYautjaIconFalconDroneBadBlood";

    [Test]
    public async Task FalconDroneUsesForegroundDrawDepth()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid deployed = default;
        EntityUid badBlood = default;
        EntityUid grate = default;
        EntityUid grateAlpha = default;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var surfaceIds = new[]
            {
                "CMUYautjaStructureHunterFloorsHunterGrate",
                "CMUYautjaStructureHunterFloorsHunterGrateAlpha",
            };

            var surfaceDepths = surfaceIds.Select(id =>
            {
                var surface = prototypes.Index<EntityPrototype>(id);
                Assert.That(surface.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
                return sprite!.DrawDepth;
            }).ToArray();

            foreach (var prototype in new[]
                     {
                         "CMUYautjaFalconDroneDeployed",
                         "CMUYautjaFalconDroneBadBloodDeployed",
                     })
            {
                var drone = prototypes.Index<EntityPrototype>(prototype);
                Assert.That(drone.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, prototype);
                Assert.Multiple(() =>
                {
                    Assert.That(sprite!.DrawDepth, Is.EqualTo((int) DrawDepth.OverMobs), prototype);
                    Assert.That(sprite.DrawDepth, Is.GreaterThan(surfaceDepths.Max()), prototype);
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            deployed = server.EntMan.SpawnEntity("CMUYautjaFalconDroneDeployed", map.GridCoords);
            badBlood = server.EntMan.SpawnEntity("CMUYautjaFalconDroneBadBloodDeployed", map.GridCoords.Offset(new Vector2(1, 0)));
            grate = server.EntMan.SpawnEntity("CMUYautjaStructureHunterFloorsHunterGrate", map.GridCoords.Offset(new Vector2(2, 0)));
            grateAlpha = server.EntMan.SpawnEntity("CMUYautjaStructureHunterFloorsHunterGrateAlpha", map.GridCoords.Offset(new Vector2(3, 0)));
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            foreach (var uid in new[] { deployed, badBlood })
            {
                var clientUid = client.EntMan.GetEntity(server.EntMan.GetNetEntity(uid));
                Assert.That(client.EntMan.TryGetComponent<SpriteComponent>(clientUid, out var sprite), Is.True);
                Assert.That(sprite!.DrawDepth, Is.EqualTo((int) DrawDepth.OverMobs));
            }

            foreach (var uid in new[] { grate, grateAlpha })
            {
                var clientUid = client.EntMan.GetEntity(server.EntMan.GetNetEntity(uid));
                Assert.That(client.EntMan.TryGetComponent<SpriteComponent>(clientUid, out var sprite), Is.True);
                Assert.That(sprite!.DrawDepth, Is.LessThan((int) DrawDepth.OverMobs));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneIsHiddenFromOrdinaryHumansButVisibleToYautjaAndGhosts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        EntityUid falcon = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                falcon = entMan.SpawnEntity(DeployedFalconPrototypeId, map.GridCoords.Offset(new Vector2(1, 0)));
                server.PlayerMan.SetAttachedEntity(session, human);
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var clientFalcon = client.EntMan.GetEntity(server.EntMan.GetNetEntity(falcon));
                AssertFalconLayerVisible(client, clientFalcon, false);
            });

            await server.WaitPost(() => server.EntMan.EnsureComponent<YautjaComponent>(human));
            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var clientFalcon = client.EntMan.GetEntity(server.EntMan.GetNetEntity(falcon));
                AssertFalconLayerVisible(client, clientFalcon, true);
            });

            await client.WaitPost(() =>
            {
                var local = client.ResolveDependency<IPlayerManager>().LocalEntity!.Value;
                client.EntMan.RemoveComponent<YautjaComponent>(local);
                client.EntMan.EnsureComponent<GhostComponent>(local);
            });
            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var clientFalcon = client.EntMan.GetEntity(server.EntMan.GetNetEntity(falcon));
                AssertFalconLayerVisible(client, clientFalcon, true);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (!server.EntMan.Deleted(human))
                    server.EntMan.DeleteEntity(human);
                if (!server.EntMan.Deleted(falcon))
                    server.EntMan.DeleteEntity(falcon);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void AssertFalconLayerVisible(
        Robust.UnitTesting.RobustIntegrationTest.ClientIntegrationInstance client,
        EntityUid falcon,
        bool visible)
    {
        Assert.That(client.EntMan.TryGetComponent<SpriteComponent>(falcon, out var sprite), Is.True);
        var spriteSystem = client.EntMan.System<SpriteSystem>();
        Assert.That(spriteSystem.LayerMapTryGet((falcon, sprite!), YautjaFalconVisualLayers.Base, out var layer, true), Is.True);
        Assert.That(sprite![layer].Visible, Is.EqualTo(visible));
    }

    [Test]
    public async Task FalconDroneZRenderUsesTheMapOccupiedByTheEye()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var lowerMap = await pair.CreateTestMap();
        var upperMap = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid network = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var zLevels = entMan.System<CMUZLevelsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                var zNetwork = zLevels.CreateZNetwork();
                network = zNetwork.Owner;

                Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [lowerMap.MapUid] = 0,
                    [upperMap.MapUid] = 1,
                }), Is.True);

                hunter = SpawnControllingHunter(
                    entMan,
                    inventory,
                    lowerMap.GridCoords,
                    entity => server.PlayerMan.SetAttachedEntity(session, entity),
                    out bracer,
                    out falcon,
                    out drone);

                transform.SetCoordinates(drone, upperMap.GridCoords);
                Assert.That(entMan.GetComponent<TransformComponent>(drone).MapUid, Is.EqualTo(upperMap.MapUid));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var eye = client.ResolveDependency<IEyeManager>().CurrentEye;
                Assert.That(eye.Position.MapId, Is.EqualTo(upperMap.MapId));

                var ui = client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>();
                Assert.That(ui.MainViewport.Viewport, Is.Not.Null);
                var scaling = new ScalingViewport
                {
                    RenderZLevels = true,
                    Eye = eye,
                };

                // Initialize the same cached systems used by the normal draw pass,
                // then query the private resolver used to select its base map.
                var renderPass = typeof(ScalingViewport).GetMethod(
                    "RenderZLevelPasses",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(renderPass, Is.Not.Null);
                renderPass!.Invoke(scaling, new object[] { ui.MainViewport.Viewport });

                var resolver = typeof(ScalingViewport).GetMethod(
                    "TryGetZLevelViewEntity",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(resolver, Is.Not.Null);
                var args = new object[] { eye, null!, null!, null! };
                Assert.That(resolver!.Invoke(scaling, args), Is.EqualTo(true));
                var renderXform = args[3] as TransformComponent;
                Assert.That(renderXform, Is.Not.Null);
                Assert.That(renderXform!.MapID, Is.EqualTo(upperMap.MapId),
                    "Z passes must use the map occupied by the targeted Falcon, not the controller's old map.");
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.SingleOrDefault();
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);

                DeleteAll(entMan, hunter, bracer, falcon, drone, network);
            });
        }

        await pair.CleanReturnAsync();
    }

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
            var ladderCoordinates = lowerMap.GridCoords.Offset(Vector2.UnitX);
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
    public async Task FalconDroneUsesOneDedicatedShoulderSpriteLayerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();

            foreach (var (id, state) in new[]
                     {
                         ("CMUYautjaFalconDrone", "falcon_drone"),
                         ("CMUYautjaFalconDroneBadBlood", "falcon_drone_badblood"),
                     })
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.Components.TryGetValue("Clothing", out var clothing), Is.True, id);
                var clothingComponent = (ClothingComponent) clothing.Component;
                var layers = clothingComponent.ClothingVisuals["ears"];
                Assert.That(layers, Has.Count.EqualTo(1),
                    $"{id} must define the source item_icons shoulder layer instead of reusing the world sprite.");
                Assert.That(layers[0].State, Is.EqualTo(state), id);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneEquippedToEarExaminesOnShoulderLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var examiner = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", MapCoordinates.Nullspace);
            var badBloodFalcon = entMan.SpawnEntity("CMUYautjaFalconDroneBadBlood", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(hunter, falcon, "ears", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, badBloodFalcon, "ears2", silent: true, force: true), Is.True);

                var markup = examine.GetExamineText(hunter, examiner).ToMarkup();

                Assert.That(markup, Does.Contain("falcon drone"));
                Assert.That(markup, Does.Contain("on their shoulder"),
                    "CMSS13 /obj/item/falcon_drone/get_examine_location() reports ear-worn falcons on the wearer's shoulder.");
                Assert.That(markup, Does.Not.Contain("ear"),
                    "CMSS13 overrides the generic ear-slot examine location for falcon drones.");
            }
            finally
            {
                DeleteAll(entMan, hunter, examiner, falcon, badBloodFalcon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneUnequipRemovesControlActionLikeCmss13Dropped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<SharedActionsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);
                AssertActionPresent(entMan, actions, hunter, "CMUActionYautjaFalconControl",
                    "CMSS13 /obj/item/falcon_drone/equipped() grants the control action on ear slots.");

                Assert.That(inventory.TryUnequip(hunter, "ears2", out _, silent: true, force: true), Is.True);

                AssertActionAbsent(entMan, actions, hunter, "CMUActionYautjaFalconControl",
                    "CMSS13 /obj/item/falcon_drone/dropped() removes /datum/action/predator_action/mask/control_falcon_drone.");
            }
            finally
            {
                DeleteAll(entMan, hunter, falcon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneRelaysNearbySpeechToControllerLikeCmss13HearTalk()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid speaker = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
                speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                metadata.SetEntityName(speaker, "Marine Spotter");
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));
                var drone = entMan.GetComponent<EyeComponent>(hunter).Target!.Value;
                Assert.That(entMan.HasComponent<YautjaFalconDroneDeployedComponent>(drone), Is.True);
                Assert.That(entMan.HasComponent<ActiveListenerComponent>(falcon), Is.True,
                    "CMSS13 /obj/item/falcon_drone uses USES_HEARING and hears while carried by the deployed hologram.");

                entMan.EventBus.RaiseLocalEvent(falcon, new ListenEvent("prey spotted?", speaker));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var history = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<ChatUIController>()
                    .History
                    .Select(entry => entry.Msg)
                    .ToList();
                var questionVerb = Loc.GetString("chat-speech-verb-question");
                var defaultVerb = Loc.GetString("chat-speech-verb-default");
                var joinedHistory = string.Join("\n", history.Select(message => $"{message.Channel}: {message.Message}"));

                Assert.That(
                    history.Any(message =>
                        message.Message.Contains("Falcon Relay:", StringComparison.OrdinalIgnoreCase) &&
                        message.Message.Contains($"Marine Spotter {questionVerb},", StringComparison.OrdinalIgnoreCase) &&
                        message.Message.Contains("\"prey spotted?\"", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 falcon hear_talk() relays nearby speech privately to the controller with the source speech verb.\nActual chat history:\n{joinedHistory}");
                Assert.That(
                    history.Any(message =>
                        message.Message.Contains($"Marine Spotter {defaultVerb},", StringComparison.OrdinalIgnoreCase) &&
                        message.Message.Contains("\"prey spotted?\"", StringComparison.OrdinalIgnoreCase)),
                    Is.False,
                    $"CMSS13 falcon hear_talk() preserves the incoming speech verb instead of hard-coding 'says'.\nActual chat history:\n{joinedHistory}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, bracer, falcon, speaker);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneHearTalkSkipsControllerAndNonHumansLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid observer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
                observer = entMan.SpawnEntity("MobObserver", map.GridCoords.Offset(new Vector2(1, 0)));

                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));
                var drone = entMan.GetComponent<EyeComponent>(hunter).Target!.Value;
                Assert.That(entMan.HasComponent<YautjaFalconDroneDeployedComponent>(drone), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new ListenEvent("controller chatter", hunter));
                entMan.EventBus.RaiseLocalEvent(falcon, new ListenEvent("ghost chatter", observer));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var history = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<ChatUIController>()
                    .History
                    .Select(entry => entry.Msg)
                    .ToList();
                var joinedHistory = string.Join("\n", history.Select(message => $"{message.Channel}: {message.Message}"));

                Assert.That(
                    history.Any(message =>
                        message.Message.Contains("controller chatter", StringComparison.OrdinalIgnoreCase) ||
                        message.Message.Contains("ghost chatter", StringComparison.OrdinalIgnoreCase)),
                    Is.False,
                    $"CMSS13 falcon hear_talk() returns for the controller and non-human sources.\nActual chat history:\n{joinedHistory}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, bracer, falcon, observer);
            });
        }

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

                emp.DoEmpEffects(drone, 1000, 30);
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

    [Test]
    public async Task FalconDroneDeployedFixtureBlocksWalls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var deployed = prototypes.Index<EntityPrototype>(DeployedFalconPrototypeId);

            Assert.That(deployed.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True);
            Assert.That(fixtures!.Fixtures.Values.All(fixture => fixture.Hard), Is.True,
                "The deployed Falcon fixture must resolve collisions instead of passing through walls.");
            Assert.That(fixtures.Fixtures.Values.All(fixture =>
                    (fixture.CollisionMask & (int) CollisionGroup.Impassable) != 0),
                Is.True,
                "The deployed Falcon collision mask must include impassable wall fixtures.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneHudMetadataDoesNotUseWorldStatusIconOverlay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid falcon = default;
        EntityUid badBloodFalcon = default;
        EntityUid? previousAttached = null;
        NetEntity hunterNet = default;
        NetEntity falconNet = default;
        NetEntity badBloodFalconNet = default;

        try
        {
            await client.WaitAssertion(() =>
            {
                var prototypes = client.ResolveDependency<IPrototypeManager>();
                Assert.That(
                    prototypes.TryIndex<HealthIconPrototype>(FalconHudIconPrototypeId, out var falconIcon),
                    Is.True,
                    "CMSS13 /mob/hologram/falcon hud_possible = list(HUNTER_HUD) and med_hud_set_status() uses falcon_drone_active.");
                Assert.That(
                    prototypes.TryIndex<HealthIconPrototype>(BadBloodFalconHudIconPrototypeId, out var badBloodFalconIcon),
                    Is.True,
                    "CMSS13 /mob/hologram/falcon/badblood/med_hud_set_status() uses falcon_drone_badblood_active.");

                Assert.Multiple(() =>
                {
                    Assert.That(falconIcon!.Icon, Is.EqualTo(FalconHudIcon("falcon_drone_active")));
                    Assert.That(badBloodFalconIcon!.Icon, Is.EqualTo(FalconHudIcon("falcon_drone_badblood_active")));
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                falcon = entMan.SpawnEntity("CMUYautjaFalconDroneDeployed", map.GridCoords.Offset(new Vector2(1, 0)));
                badBloodFalcon = entMan.SpawnEntity("CMUYautjaFalconDroneBadBloodDeployed", map.GridCoords.Offset(new Vector2(2, 0)));

                server.PlayerMan.SetAttachedEntity(session, hunter);
                hunterNet = entMan.GetNetEntity(hunter);
                falconNet = entMan.GetNetEntity(falcon);
                badBloodFalconNet = entMan.GetNetEntity(badBloodFalcon);
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var player = client.ResolveDependency<IPlayerManager>();

                Assert.That(entMan.TryGetEntity(hunterNet, out var clientHunter), Is.True);
                Assert.That(entMan.TryGetEntity(falconNet, out var clientFalcon), Is.True);
                Assert.That(entMan.TryGetEntity(badBloodFalconNet, out var clientBadBloodFalcon), Is.True);
                Assert.That(player.LocalEntity, Is.EqualTo(clientHunter));

                entMan.EnsureComponent<YautjaHudViewerComponent>(clientHunter!.Value);
                Assert.That(entMan.HasComponent<StatusIconComponent>(clientFalcon!.Value), Is.False,
                    "StatusIconOverlay draws the full directional Falcon sprite above the entity as a south-facing duplicate.");
                Assert.That(entMan.HasComponent<StatusIconComponent>(clientBadBloodFalcon!.Value), Is.False,
                    "Bad Blood Falcons must not render their full directional sprite through StatusIconOverlay either.");

                var statusIcons = entMan.System<StatusIconSystem>();
                var baseStates = HudIconStates(statusIcons.GetStatusIcons(clientFalcon.Value));
                var badBloodStates = HudIconStates(statusIcons.GetStatusIcons(clientBadBloodFalcon.Value));

                Assert.Multiple(() =>
                {
                    Assert.That(baseStates, Does.Contain("falcon_drone_active"));
                    Assert.That(badBloodStates, Does.Contain("falcon_drone_badblood_active"));
                });

                entMan.RemoveComponent<YautjaHudViewerComponent>(clientHunter.Value);
                var directIcons = new List<StatusIconData>();
                var ev = new GetStatusIconsEvent(directIcons);
                entMan.EventBus.RaiseLocalEvent(clientFalcon.Value, ref ev);
                Assert.That(directIcons, Is.Empty,
                    "CMSS13 Falcon HUD entries are on HUNTER_HUD; locally they should stay hidden without a Yautja HUD viewer.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, falcon, badBloodFalcon);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneControlFailurePathsMatchCmss13CanControlFalconDrone()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid criticalHunter = default;
        EntityUid criticalBracer = default;
        EntityUid criticalFalcon = default;
        EntityUid nonHumanTechUser = default;
        EntityUid nonHumanFalcon = default;
        EntityUid bracerlessTechUser = default;
        EntityUid bracerlessFalcon = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                criticalHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                criticalBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                criticalFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(criticalHunter);
                Assert.That(inventory.TryEquip(criticalHunter, criticalBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(criticalHunter, MobState.Critical);
                server.PlayerMan.SetAttachedEntity(session, criticalHunter);

                entMan.EventBus.RaiseLocalEvent(criticalFalcon, new UseInHandEvent(criticalHunter));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(criticalHunter), Is.False,
                        "CMSS13 /obj/item/falcon_drone/can_control_falcon_drone() returns immediately for is_mob_incapacitated().");
                    Assert.That(CountPrototype(entMan, "CMUYautjaFalconDroneDeployed"), Is.EqualTo(0),
                        "Incapacitated users must not deploy a Falcon hologram.");
                });
            });

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Not.Contain("You do not know how to use this."),
                        $"CMSS13 returns silently for incapacitated Falcon users before the non-tech denial popup.\nActual popups:\n{string.Join("\n", labels)}");
                    Assert.That(labels.Any(label => label.StartsWith("You need your bracers to control", StringComparison.Ordinal)), Is.False,
                        $"CMSS13 returns silently for incapacitated Falcon users before the bracer denial popup.\nActual popups:\n{string.Join("\n", labels)}");
                    Assert.That(labels, Does.Not.Contain("The falcon drone takes flight and streams its sight to your mask."),
                        $"CMSS13 returns silently for incapacitated Falcon users before deployment.\nActual popups:\n{string.Join("\n", labels)}");
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();

                nonHumanTechUser = entMan.SpawnEntity("MobObserver", map.GridCoords.Offset(new Vector2(1, 0)));
                nonHumanFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(nonHumanTechUser);
                server.PlayerMan.SetAttachedEntity(session, nonHumanTechUser);

                entMan.EventBus.RaiseLocalEvent(nonHumanFalcon, new UseInHandEvent(nonHumanTechUser));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain("You do not know how to use this."),
                    $"CMSS13 rejects non-human Falcon users before bracer checks.\nActual popups:\n{string.Join("\n", labels)}");
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();

                bracerlessTechUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                bracerlessFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords.Offset(new Vector2(2, 0)));
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bracerlessTechUser);
                server.PlayerMan.SetAttachedEntity(session, bracerlessTechUser);

                entMan.EventBus.RaiseLocalEvent(bracerlessFalcon, new UseInHandEvent(bracerlessTechUser));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain("You need your bracers to control the falcon drone!"),
                    $"CMSS13 bracerless human tech users get the source plural bracers warning.\nActual popups:\n{string.Join("\n", labels)}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                DeleteAll(entMan,
                    criticalHunter,
                    criticalBracer,
                    criticalFalcon,
                    nonHumanTechUser,
                    nonHumanFalcon,
                    bracerlessTechUser,
                    bracerlessFalcon);
                DeleteByPrototype(entMan, "CMUYautjaFalconDroneDeployed");
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

    private static SpriteSpecifier.Rsi FalconHudIcon(string state)
    {
        return new SpriteSpecifier.Rsi(new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi"), state);
    }

    private static List<string> HudIconStates(IReadOnlyList<StatusIconData> icons)
    {
        return icons
            .Select(icon => icon.Icon)
            .OfType<SpriteSpecifier.Rsi>()
            .Select(icon => icon.RsiState)
            .ToList();
    }

    private static void AssertActionPresent(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid holder,
        string prototype,
        string message)
    {
        Assert.That(ActionPrototypeIds(entMan, actions, holder), Does.Contain(prototype), message);
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
