using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Marines.HyperSleep;
using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Light.Components;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage.Components;
using Content.Shared.VendingMachines;
using Content.Shared.Verbs;
using Content.Server._CMU14.Light;
using Content.Server.Medical;
using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Client.Popups;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Content.Shared.Power.Components;
using ServerPointLightComponent = Robust.Server.GameObjects.PointLightComponent;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests._CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipVisualRegressionTest
{
    [Test]
    public async Task HunterShipLadderWrappersDeclareCmss13Directions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var expected = new Dictionary<string, (bool Up, bool Down, int Offset)>
            {
                ["CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder01SouthOffset0x2"] = (false, true, -1),
                ["CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder10SouthOffset0x2"] = (true, false, 1),
                ["CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder11SouthOffset0x2"] = (true, true, 1),
            };

            foreach (var (id, direction) in expected)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<CMUZLevelLadderComponent>(out var ladder, factory), Is.True, id);

                var upField = typeof(CMUZLevelLadderComponent).GetField("CanMoveUp");
                var downField = typeof(CMUZLevelLadderComponent).GetField("CanMoveDown");
                Assert.Multiple(() =>
                {
                    Assert.That(upField, Is.Not.Null, "CMUZLevelLadder must expose CanMoveUp.");
                    Assert.That(downField, Is.Not.Null, "CMUZLevelLadder must expose CanMoveDown.");
                    Assert.That(ladder!.Offset, Is.EqualTo(direction.Offset), id);
                    if (upField != null)
                        Assert.That(upField.GetValue(ladder), Is.EqualTo(direction.Up), id);
                    if (downField != null)
                        Assert.That(downField.GetValue(ladder), Is.EqualTo(direction.Down), id);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipMiddleLadderCanClimbBothDirections()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var lower = await pair.CreateTestMap();
        var middle = await pair.CreateTestMap();
        var upper = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid ladder = default;
        EntityUid network = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var zNetwork = zLevels.CreateZNetwork();
            network = zNetwork.Owner;
            Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork, new Dictionary<EntityUid, int>
            {
                [lower.MapUid] = 0,
                [middle.MapUid] = 1,
                [upper.MapUid] = 2,
            }), Is.True);

            ladder = entMan.SpawnEntity(
                "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder11SouthOffset0x2",
                middle.GridCoords);
            user = entMan.SpawnEntity("CMMobHuman", middle.GridCoords);

            var verbs = new GetVerbsEvent<AlternativeVerb>(user, ladder, null, null, true, true, true, new());
            entMan.EventBus.RaiseLocalEvent(ladder, verbs, true);
            var climbUp = verbs.Verbs.SingleOrDefault(verb =>
                verb.Text == Loc.GetString("cmu-zlevel-ladder-climb-up"));
            var climbDown = verbs.Verbs.SingleOrDefault(verb =>
                verb.Text == Loc.GetString("cmu-zlevel-ladder-climb-down"));

            Assert.Multiple(() =>
            {
                Assert.That(climbUp, Is.Not.Null, "The middle Hunter Ship ladder must offer climbing up.");
                Assert.That(climbDown, Is.Not.Null, "The middle Hunter Ship ladder must offer climbing down.");
            });
            climbUp!.Act!.Invoke();
            Assert.That(transform.GetMapCoordinates(user).MapId, Is.EqualTo(middle.MapId));
        });

        await server.WaitRunTicks(130);
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            Assert.That(transform.GetMapCoordinates(user).MapId, Is.EqualTo(upper.MapId));
            transform.SetCoordinates(user, middle.GridCoords);

            var verbs = new GetVerbsEvent<AlternativeVerb>(user, ladder, null, null, true, true, true, new());
            entMan.EventBus.RaiseLocalEvent(ladder, verbs, true);
            var climbDown = verbs.Verbs.Single(verb =>
                verb.Text == Loc.GetString("cmu-zlevel-ladder-climb-down"));
            climbDown.Act!.Invoke();
        });

        await server.WaitRunTicks(130);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<SharedTransformSystem>().GetMapCoordinates(user).MapId,
                Is.EqualTo(lower.MapId));
        });

        await server.WaitPost(() =>
        {
            if (user != default && !server.EntMan.Deleted(user))
                server.EntMan.DeleteEntity(user);
            if (ladder != default && !server.EntMan.Deleted(ladder))
                server.EntMan.DeleteEntity(ladder);
            if (network != default && !server.EntMan.Deleted(network))
                server.EntMan.DeleteEntity(network);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipFlamePropsStartWithLight()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var flameProps = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.ID.StartsWith("CMUHunterShip", StringComparison.Ordinal) &&
                                proto.ID.Contains("Brazier", StringComparison.Ordinal) &&
                                !proto.ID.Contains("Frame", StringComparison.Ordinal))
                .ToArray();

            Assert.That(flameProps, Is.Not.Empty);
            foreach (var prototype in flameProps)
            {
                Assert.That(prototype.TryGetComponent<ServerPointLightComponent>(out var light, factory),
                    Is.True, prototype.ID);
                Assert.That(light!.Enabled, Is.True, prototype.ID);
                Assert.That(light.Radius, Is.GreaterThan(0), prototype.ID);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipRunesRenderAboveFloorOverlays()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var runes = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract && IsHunterShipRune(proto))
                .Where(proto => proto.TryGetComponent<SpriteComponent>(out _, factory))
                .ToArray();

            Assert.That(runes, Is.Not.Empty);
            foreach (var prototype in runes)
            {
                Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory),
                    Is.True, prototype.ID);
                Assert.That(sprite!.DrawDepth, Is.EqualTo((int) DrawDepth.HighFloorObjects), prototype.ID);
                Assert.That(sprite.AllLayers, Is.Not.Empty, prototype.ID);
                foreach (var layer in sprite.AllLayers.Cast<SpriteComponent.Layer>())
                {
                    Assert.That(layer.ShaderPrototype, Is.EqualTo(SpriteSystem.UnshadedId),
                        $"{prototype.ID} must remain visible as an emissive Rune in darkness.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipRunesHaveSourcePointLights()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var runes = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract && IsHunterShipRune(proto))
                .ToArray();

            Assert.That(runes, Is.Not.Empty);
            foreach (var prototype in runes)
            {
                Assert.That(prototype.TryGetComponent<ServerPointLightComponent>(out var light, factory),
                    Is.True, prototype.ID);
                Assert.That(light!.Enabled, Is.True, prototype.ID);
                Assert.That(light.Radius, Is.EqualTo(1.25f), prototype.ID);
                Assert.That(light.Energy, Is.EqualTo(1f), prototype.ID);
                Assert.That(light.Color, Is.EqualTo(Color.Red), prototype.ID);
                Assert.That(light.CastShadows, Is.False, prototype.ID);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipRunesHaveClientLightPrototypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var runes = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract && IsHunterShipRune(proto))
                .ToArray();

            Assert.That(runes, Is.Not.Empty);
            foreach (var prototype in runes)
            {
                Assert.That(prototype.TryGetComponent<PointLightComponent>(out var light, factory), Is.True,
                    prototype.ID);
                Assert.That(light!.Enabled, Is.True, prototype.ID);
                Assert.That(light.Radius, Is.EqualTo(1.25f), prototype.ID);
                Assert.That(light.Energy, Is.EqualTo(1f), prototype.ID);
                Assert.That(light.Color, Is.EqualTo(Color.Red), prototype.ID);
                Assert.That(light.CastShadows, Is.False, prototype.ID);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipHypersleepUsesBodyScannerClosedStateWhenOccupied()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var id in HypersleepIds)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<GenericVisualizerComponent>(out var visualizer, factory), Is.True, id);
                var full = visualizer!.Visuals[CryostorageVisuals.Full];
                var baseVisuals = full["enum.HyperSleepChamberLayers.Base"];

                Assert.Multiple(() =>
                {
                    Assert.That(baseVisuals["True"].State, Is.EqualTo("body_scanner_closed"), id);
                    Assert.That(baseVisuals["False"].State, Is.EqualTo("body_scanner_open"), id);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipCryoCellShowsOnlyOneOccupiedSpriteStack()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid cryo = default;
        EntityUid body = default;

        await server.WaitPost(() =>
        {
            cryo = server.EntMan.SpawnEntity("CMUHunterShipPlacedCryoPodPredCellSouthOffset1x16", map.GridCoords);
            body = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var cryoComp = server.EntMan.GetComponent<CryoPodComponent>(cryo);
            Assert.That(server.EntMan.System<CryoPodSystem>().InsertBody(cryo, body, cryoComp), Is.True);

            var containers = server.EntMan.GetComponent<ContainerManagerComponent>(cryo);
            Assert.That(containers.Containers["scanner-body"].ShowContents, Is.False,
                "The Yautja occupied sprite replaces the real contained mob visually.");
        });

        await pair.ReallyBeIdle(10);

        await client.WaitAssertion(() =>
        {
            var clientCryo = client.EntMan.GetEntity(server.EntMan.GetNetEntity(cryo));
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientCryo);
            var clientBody = client.EntMan.GetEntity(server.EntMan.GetNetEntity(body));
            var bodySprite = client.EntMan.GetComponent<SpriteComponent>(clientBody);
            var layers = sprite.AllLayers.ToArray();

            Assert.That(layers, Has.Length.EqualTo(3), "Cryo cell must keep one base, one cover, and one hidden panel layer.");
            Assert.That(layers.Count(layer => layer.Visible), Is.EqualTo(2),
                "An occupied cryo cell must show only its base and cover, without a stale empty-cell layer.");
            Assert.That(layers[0].RsiState.Name, Is.EqualTo("pred_cell-on-empty"));
            Assert.That(layers[1].RsiState.Name, Is.EqualTo("pred_cell-on-occupied"));
            Assert.That(layers[1].Visible, Is.True);
            Assert.That(layers[2].Visible, Is.False);
            Assert.That(bodySprite.ContainerOccluded, Is.True,
                "The real mob sprite must not render over the Yautja occupied-cell artwork.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipCryoCellVisibilityOverrideDoesNotChangeGenericCryoPod()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var hunterCryo = prototypes.Index<EntityPrototype>(
                "CMUHunterShipPlacedCryoPodPredCellSouthOffset1x16");
            var genericCryo = prototypes.Index<EntityPrototype>("CryoPod");

            Assert.That(hunterCryo.TryGetComponent<ContainerManagerComponent>(out var hunterContainers, factory),
                Is.True);
            Assert.That(genericCryo.TryGetComponent<ContainerManagerComponent>(out var genericContainers, factory),
                Is.True);
            var expectedContainerKeys = new[]
            {
                "scanner-body",
                "beakerSlot",
                "machine_board",
                "machine_parts",
            };

            Assert.Multiple(() =>
            {
                Assert.That(hunterContainers!.Containers.Keys, Is.EquivalentTo(expectedContainerKeys));
                Assert.That(genericContainers!.Containers.Keys, Is.EquivalentTo(expectedContainerKeys));

                foreach (var key in expectedContainerKeys)
                {
                    var hunterContainer = hunterContainers.Containers[key];
                    var genericContainer = genericContainers.Containers[key];

                    Assert.That(hunterContainer.GetType(), Is.EqualTo(genericContainer.GetType()), key);
                    if (key == "scanner-body")
                    {
                        Assert.That(hunterContainer.ShowContents, Is.False);
                        Assert.That(genericContainer.ShowContents, Is.True);
                    }
                    else
                    {
                        Assert.That(hunterContainer.ShowContents, Is.EqualTo(genericContainer.ShowContents), key);
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipLanternsStartEnabled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid spawnedLantern = default;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var lanterns = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.ID.StartsWith("CMUHunterShipPlacedRMCFlashlightLanternYautja", StringComparison.Ordinal))
                .ToArray();

            Assert.That(lanterns, Is.Not.Empty);
            foreach (var prototype in lanterns)
            {
                Assert.That(prototype.TryGetComponent<ServerPointLightComponent>(out var light, factory), Is.True, prototype.ID);
                Assert.That(light!.Enabled, Is.True, prototype.ID);
                Assert.That(prototype.TryGetComponent<CMUStartHandheldLightOnComponent>(out _, factory), Is.True,
                    prototype.ID);
            }

            spawnedLantern = server.EntMan.SpawnEntity(lanterns[0].ID, map.GridCoords);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var light = server.EntMan.GetComponent<HandheldLightComponent>(spawnedLantern);
            Assert.That(light.Activated, Is.True, "Hunter ship lantern must be lit after map initialization.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipGearRacksDoNotRenderAnUnshadedDuplicate()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var racks = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract &&
                                proto.TryGetComponent<YautjaGearRackComponent>(out _, factory) &&
                                proto.TryGetComponent<SpriteComponent>(out _, factory))
                .ToArray();

            Assert.That(racks, Is.Not.Empty);
        });

        var server = pair.Server;
        var serverRack = default(EntityUid);
        var map = await pair.CreateTestMap();
        await server.WaitPost(() =>
        {
            serverRack = server.EntMan.SpawnEntity(
                "CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorLeftSouthOffset0x16",
                map.GridCoords);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var uid = client.EntMan.GetEntity(server.EntMan.GetNetEntity(serverRack));
            var sprite = client.EntMan.GetComponent<SpriteComponent>(uid);
            Assert.That(client.EntMan.System<SpriteSystem>().LayerMapTryGet((uid, sprite),
                    VendingMachineVisualLayers.BaseUnshaded, out var layer, false), Is.True);
            Assert.That(sprite.AllLayers.ElementAt(layer).Visible, Is.False, uid.ToString());
        });

        await server.WaitPost(() =>
        {
            if (!server.EntMan.Deleted(serverRack))
                server.EntMan.DeleteEntity(serverRack);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipDoorButtonsArePoweredByDefault()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var buttonIds = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract &&
                                proto.ID.StartsWith("CMUHunterShipPlacedRMCPodDoorButton", StringComparison.Ordinal))
                .Select(proto => proto.ID)
                .ToArray();

            Assert.That(buttonIds, Is.Not.Empty);
            foreach (var (id, index) in buttonIds.Select((id, index) => (id, index)))
            {
                var button = server.EntMan.SpawnEntity(id, map.GridCoords.Offset(new Vector2(index, 0)));
                spawned.Add(button);
            }
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            foreach (var button in spawned)
            {
                var receiver = server.EntMan.GetComponent<ApcPowerReceiverComponent>(button);
                Assert.That(receiver.NeedsPower, Is.False, button.ToString());
                Assert.That(receiver.Powered, Is.True,
                    "Hunter Ship door buttons must not report the generic unpowered message.");
            }
        });

        await server.WaitPost(() =>
        {
            foreach (var button in spawned)
            {
                if (!server.EntMan.Deleted(button))
                    server.EntMan.DeleteEntity(button);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipSelfPoweredDoorButtonIgnoresStalePowerState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid button = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                button = entMan.SpawnEntity(
                    "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffset0x23",
                    map.GridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<AccessComponent>(hunter).Tags.Add("CMUAccessYautjaElder");
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var power = entMan.GetComponent<ApcPowerReceiverComponent>(button);
                Assert.That(power.NeedsPower, Is.False);
                power.Powered = false;
                entMan.Dirty(button, power);
                entMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(hunter, button, true));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Not.Contain("It does not appear to be working."),
                    $"Self-powered Hunter Ship buttons must not show an unpowered popup. Actual popups:\n{string.Join("\n", labels)}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), previousAttached);
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (button != default && !entMan.Deleted(button))
                    entMan.DeleteEntity(button);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipFoodStorageContainsEdibleFood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid locker = default;
        EntityUid firstFood = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            locker = entMan.SpawnEntity("CMUHunterShipMeatLockerFoodMeatx16", map.GridCoords);
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
            var storage = entMan.GetComponent<EntityStorageComponent>(locker);

            Assert.That(storage.Contents.ContainedEntities, Has.Count.EqualTo(16));
            firstFood = storage.Contents.ContainedEntities.First();

            foreach (var item in storage.Contents.ContainedEntities)
            {
                Assert.That(entMan.HasComponent<FoodComponent>(item), Is.True);
                Assert.That(entMan.GetComponent<FoodComponent>(item).UseSound, Is.Not.Null);
            }

            foreach (var id in new[]
                     {
                         "CMUHunterShipPlacedFoodBreadMeatXenoSliceXenobreadsliceSouthOffset5x4",
                         "CMUHunterShipPlacedFoodMeatMeatSouthOffset0x5",
                         "CMUHunterShipPlacedFoodPieXenoXenomeatpieSouth",
                     })
            {
                var placed = entMan.SpawnEntity(id, map.GridCoords.Offset(new Vector2(2, 0)));
                Assert.That(entMan.HasComponent<FoodComponent>(placed), Is.True);
                Assert.That(entMan.GetComponent<FoodComponent>(placed).UseSound, Is.Not.Null, id);
            }
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storage = entMan.GetComponent<EntityStorageComponent>(locker);
            var food = entMan.System<FoodSystem>();
            var storageSystem = entMan.System<EntityStorageSystem>();
            var interaction = entMan.System<SharedInteractionSystem>();

            Assert.That(food.IsDigestibleBy(hunter, firstFood), Is.True);
            Assert.That(storageSystem.Remove(firstFood, locker, storage), Is.True);
            var hunterXform = entMan.GetComponent<TransformComponent>(hunter);
            var foodXform = entMan.GetComponent<TransformComponent>(firstFood);
            Assert.That(food.GetUsesRemaining(firstFood), Is.GreaterThan(0));
            Assert.That(food.IsMouthBlocked(hunter), Is.False);
            Assert.That(interaction.InRangeUnobstructed(hunter, firstFood), Is.True,
                $"hunter={hunterXform.Coordinates} food={foodXform.Coordinates}");
            var result = food.TryFeed(hunter, hunter, firstFood, entMan.GetComponent<FoodComponent>(firstFood));
            Assert.That(result.Success, Is.True,
                $"handled={result.Handled} hunter={hunterXform.Coordinates} food={foodXform.Coordinates}");
        });

        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityQuery<AudioComponent>().Any(), Is.True,
                "Eating food must create an audio entity on the hunter ship.");
        });

        await pair.CleanReturnAsync();
    }

    private static readonly string[] HypersleepIds =
    [
        "CMUHunterShipPlacedMedicalScannerBodyScannerOpenSouth",
        "CMUHunterShipPlacedMedicalScannerBodyScannerOpenWest",
    ];

    private static bool IsHunterShipRune(EntityPrototype prototype)
    {
        return prototype.ID.StartsWith("CMUHunterShipObjEffectHunterRune", StringComparison.Ordinal) ||
               prototype.ID.StartsWith("CMUHunterShipPlacedObjEffectHunterRune", StringComparison.Ordinal);
    }
}
