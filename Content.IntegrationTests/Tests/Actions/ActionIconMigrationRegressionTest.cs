#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ClientActionsSystem = Content.Client.Actions.ActionsSystem;
using ServerActionsSystem = Content.Server.Actions.ActionsSystem;

namespace Content.IntegrationTests.Tests.Actions;

[TestFixture]
[TestOf(typeof(SharedActionsSystem))]
public sealed class ActionIconMigrationRegressionTest : GameTest
{
    private static readonly string[] EffectiveActionRoots =
    [
        "ActionXenoBase",
        "ActionZombieSummonerOpen",
        "ActionAbominationSpiderLeap",
        "ActionMarineBase",
        "ActionVehicleToggleView",
        "ActionVehicleLock",
    ];

    [Test]
    public async Task MigratedRootsStylesAndCustomBackgroundsRetainTheirContracts()
    {
        await Server.WaitAssertion(() =>
        {
            var fields = typeof(ActionComponent)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.Name)
                .ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(fields, Does.Not.Contain("Icon"));
                Assert.That(fields, Does.Not.Contain("IconOn"));
                Assert.That(fields, Does.Not.Contain("IconColor"));
                Assert.That(fields, Does.Contain(nameof(ActionComponent.BackgroundOn)));

                AssertBackground("ActionXenoBase", "template_active_big");
                AssertBackground("ActionZombieSummonerOpen", "template_active_big");
                AssertBackground("ActionAbominationSpiderLeap", "template_active_big");
                AssertBackground("RMCActionToggleRecoil", "template_on_big");

                AssertStyle("ActionVehicleToggleView", ItemActionIconStyle.BigAction);
                AssertStyle("ActionVehicleLock", ItemActionIconStyle.BigItem);
                AssertStyle("ActionToggleVulpkaninWagging", ItemActionIconStyle.NoItem);
            });
        });

        await Client.WaitAssertion(() =>
        {
            foreach (var id in EffectiveActionRoots)
            {
                var prototype = CProtoMan.Index<EntityPrototype>(id);
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryComp<SpriteComponent>(out _, CEntMan.ComponentFactory), Is.True,
                        $"{id} Sprite");
                    Assert.That(prototype.TryComp<AppearanceComponent>(out _, CEntMan.ComponentFactory), Is.True,
                        $"{id} Appearance");
                    Assert.That(prototype.TryComp<GenericVisualizerComponent>(out _, CEntMan.ComponentFactory), Is.True,
                        $"{id} GenericVisualizer");
                });
            }
        });
    }

    [TestCase("ActionSleep", "/Textures/Clothing/Head/Hats/pyjamasyndicatered.rsi", "icon")]
    [TestCase("CMUActionCargoVehicleReturn", "/Textures/_RMC14/Objects/Devices/command_tablet.rsi", "cotablet")]
    [TestCase("CMUActionCargoVehicleToggleBay", "/Textures/CMU14/Structures/vehicles/cargo_carrier.rsi", "cargo_open")]
    [TestCase("CMUActionCargoVehicleSelfDestruct", "/Textures/_RMC14/Objects/Weapons/Grenades/rcm20mm/he.rsi", "icon")]
    public async Task StaticIconsUseVisibleFirstSpriteLayer(string prototype, string rsi, string state)
    {
        var map = await Pair.CreateTestMap();
        EntityUid serverAction = default;
        NetEntity netAction = default;

        try
        {
            await Server.WaitPost(() =>
            {
                serverAction = SEntMan.SpawnEntity(prototype, map.GridCoords);
                netAction = SEntMan.GetNetEntity(serverAction);
            });
            await Pair.RunUntilSynced();

            await Client.WaitAssertion(() =>
            {
                var action = CEntMan.GetEntity(netAction);
                var sprite = CEntMan.GetComponent<SpriteComponent>(action);
                var sprites = Client.System<SpriteSystem>();

                Assert.That(sprites.LayerMapTryGet(
                    (action, sprite),
                    ActionVisuals.Icon,
                    out var iconLayer,
                    false), Is.True);
                Assert.That(iconLayer, Is.Zero);
                AssertRsiLayer(sprites, action, ActionVisuals.Icon, rsi, state, visible: true);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(serverAction))
                    SEntMan.DeleteEntity(serverAction);
            });
        }
    }

    [Test]
    public async Task StaticAndDynamicIconsReplicateAndCustomBackgroundTracksActionState()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        var serverEntities = new Dictionary<string, EntityUid>();
        var netEntities = new Dictionary<string, NetEntity>();
        string[] prototypes =
        [
            "CMActionToggleAttachable",
            "ActionVehicleToggleView",
            "ActionVehicleLock",
            "ActionToggleVulpkaninWagging",
            "RMCActionViewIntelObjectives",
            "ActionMarineCallToAttention",
            "ActionWendigoVoice",
            "ActionXenoRest",
        ];

        try
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, map.Grid.Owner);
                foreach (var prototype in prototypes)
                {
                    var uid = SEntMan.SpawnEntity(prototype, map.GridCoords);
                    serverEntities.Add(prototype, uid);
                    netEntities.Add(prototype, SEntMan.GetNetEntity(uid));
                }
            });
            await Pair.RunUntilSynced();

        var initialDynamicLayer = -1;
        var initialVehicleToggledLayer = -1;
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            var vehicle = ClientEntity("ActionVehicleToggleView");

            AssertRsiLayer(spriteSystem, ClientEntity("ActionMarineCallToAttention"), ActionVisuals.Icon,
                "/Textures/Actions/GOVFOR/call_to_attention.rsi", "attention", visible: true);
            AssertRsiLayer(spriteSystem, ClientEntity("RMCActionViewIntelObjectives"), ActionVisuals.Icon,
                "/Textures/_RMC14/Objects/Misc/paper.rsi", "folder_white", visible: true);
            AssertTextureLayer(spriteSystem, ClientEntity("ActionWendigoVoice"), ActionVisuals.Icon,
                "/Textures/Interface/Actions/scream.png", visible: true);
            AssertRsiLayer(spriteSystem, ClientEntity("ActionToggleVulpkaninWagging"), ActionVisuals.Icon,
                "/Textures/_RMC14/Mobs/Vulpkanin/tail_markings.rsi", "tail-wag-icon", visible: true);
            var hasReservedLayer = spriteSystem.LayerMapTryGet(
                dynamicAction,
                ActionVisuals.IconToggled,
                out initialDynamicLayer,
                false);
            Assert.That(Client.System<AppearanceSystem>().TryGetData<SpriteSpecifier>(
                dynamicAction,
                ActionState.DynamicIconToggled,
                out _), Is.False);
            if (hasReservedLayer)
            {
                var reserved = GetLayer(spriteSystem, dynamicAction, ActionVisuals.IconToggled);
                Assert.Multiple(() =>
                {
                    Assert.That(reserved.RsiState.IsValid, Is.False);
                    Assert.That(reserved.Texture, Is.Null);
                    Assert.That(reserved.Visible, Is.False);
                });
            }
            var wag = ClientEntity("ActionToggleVulpkaninWagging");
            if (spriteSystem.LayerMapTryGet(wag, ActionVisuals.IconToggled, out _, false))
            {
                var collapsed = GetLayer(spriteSystem, wag, ActionVisuals.IconToggled);
                Assert.Multiple(() =>
                {
                    Assert.That(collapsed.RsiState.IsValid, Is.False);
                    Assert.That(collapsed.Texture, Is.Null,
                        "identical legacy icon/iconOn pairs must not create a second rendered icon");
                });
            }
            AssertTextureLayer(spriteSystem, vehicle, ActionVisuals.Icon,
                "/Textures/Interface/Actions/eyeopen.png", visible: true);
            AssertTextureLayer(spriteSystem, vehicle, ActionVisuals.IconToggled,
                "/Textures/Interface/Actions/eyeclose.png", visible: false);
            Assert.That(spriteSystem.LayerMapTryGet(
                vehicle,
                ActionVisuals.IconToggled,
                out initialVehicleToggledLayer,
                false), Is.True);
        });

        var dynamic = serverEntities["CMActionToggleAttachable"];
        var vehicleServer = serverEntities["ActionVehicleToggleView"];
        var rsiIcon = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_RMC14/Actions/id_lock_actions.rsi"),
            "id_lock_locked");
        var textureIcon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Actions/scream.png"));
        var entityIcon = new SpriteSpecifier.EntityPrototype("CMFolderWhite");

        await Server.WaitPost(() =>
        {
            var actions = Server.System<ServerActionsSystem>();
            actions.SetIcon(dynamic, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/click.png")));
            actions.SetIconColor(dynamic, Color.Cyan);
            actions.SetIconOn(dynamic, rsiIcon);
        });
        await Pair.RunUntilSynced();

        var dynamicLayer = -1;
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            AssertTextureLayer(spriteSystem, dynamicAction, ActionVisuals.Icon,
                "/Textures/Interface/Emotes/click.png", visible: true, color: Color.Cyan);
            Assert.That(spriteSystem.LayerMapTryGet(
                dynamicAction,
                ActionVisuals.IconToggled,
                out dynamicLayer,
                false), Is.True);
            if (initialDynamicLayer >= 0)
                Assert.That(dynamicLayer, Is.EqualTo(initialDynamicLayer));
            AssertRsiLayer(spriteSystem, dynamicAction, ActionVisuals.IconToggled,
                "/Textures/_RMC14/Actions/id_lock_actions.rsi", "id_lock_locked", visible: false,
                color: Color.Cyan);
        });

        await Server.WaitPost(() => Server.System<ServerActionsSystem>().SetToggled(dynamic, true));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            Assert.That(GetLayer(spriteSystem, dynamicAction, ActionVisuals.Icon).Visible, Is.False);
            Assert.That(GetLayer(spriteSystem, dynamicAction, ActionVisuals.IconToggled, out var layer).Visible, Is.True);
            Assert.That(layer, Is.EqualTo(dynamicLayer));
        });

        await Server.WaitPost(() => Server.System<ServerActionsSystem>().SetIconOn(dynamic, textureIcon));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            AssertTextureLayer(spriteSystem, dynamicAction, ActionVisuals.IconToggled,
                "/Textures/Interface/Actions/scream.png", visible: true, expectedLayer: dynamicLayer);
        });

        await Server.WaitPost(() => Server.System<ServerActionsSystem>().SetIconOn(dynamic, entityIcon));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            var layer = GetLayer(spriteSystem, dynamicAction, ActionVisuals.IconToggled, out var index);
            Assert.Multiple(() =>
            {
                Assert.That(index, Is.EqualTo(dynamicLayer));
                Assert.That(layer.Visible, Is.True);
                Assert.That(layer.Texture, Is.SameAs(spriteSystem.Frame0(entityIcon)));
                Assert.That(layer.Color, Is.EqualTo(Color.Cyan));
            });
        });

        await Server.WaitPost(() =>
        {
            var actions = Server.System<ServerActionsSystem>();
            actions.SetToggled(dynamic, false);
            actions.SetIconOn(dynamic, null);

            actions.SetIconColor(vehicleServer, Color.Magenta);
            actions.SetToggled(vehicleServer, true);
            actions.SetIconOn(vehicleServer, null);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            var vehicle = ClientEntity("ActionVehicleToggleView");
            Assert.Multiple(() =>
            {
                Assert.That(Client.System<AppearanceSystem>().TryGetData<SpriteSpecifier>(
                    dynamicAction,
                    ActionState.DynamicIconToggled,
                    out _), Is.False, "null SetIconOn must remove its appearance override");
                var cleared = GetLayer(spriteSystem, dynamicAction, ActionVisuals.IconToggled, out var clearedLayer);
                Assert.That(clearedLayer, Is.EqualTo(dynamicLayer));
                Assert.That(cleared.RsiState.IsValid, Is.False);
                Assert.That(cleared.Texture, Is.Null,
                    "the reserved dynamic layer must no longer render the prior icon");
                Assert.That(GetLayer(spriteSystem, vehicle, ActionVisuals.Icon).Visible, Is.False);
                Assert.That(GetLayer(spriteSystem, vehicle, ActionVisuals.IconToggled, out var layer).Visible, Is.True);
                Assert.That(layer, Is.EqualTo(initialVehicleToggledLayer),
                    "static toggled layers must retain their original map");
                Assert.That(GetLayer(spriteSystem, vehicle, ActionVisuals.IconToggled).Color, Is.EqualTo(Color.Magenta));
            });
        });

        await Client.WaitAssertion(() =>
        {
            var actions = Client.System<ClientActionsSystem>();
            var spriteSystem = Client.System<SpriteSystem>();
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var controller = ui.GetUIController<ActionUIController>();
            var selecting = typeof(ActionUIController).GetProperty(
                nameof(ActionUIController.SelectingTargetFor),
                BindingFlags.Instance | BindingFlags.Public)!;
            var button = new ActionButton(CEntMan, controller);
            var xeno = ClientEntity("ActionXenoRest");
            var vehicle = ClientEntity("ActionVehicleToggleView");
            var lockAction = ClientEntity("ActionVehicleLock");
            var wag = ClientEntity("ActionToggleVulpkaninWagging");

            try
            {
                selecting.SetValue(controller, null);
                button.UpdateData(xeno, actions);
                var theme = button.Button.Texture;
                var custom = spriteSystem.Frame0(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/_RMC14/Actions/xeno_actions.rsi"),
                    "template_active_big"));
                Assert.That(theme, Is.Not.Null);
                Assert.That(theme, Is.Not.SameAs(custom));

                selecting.SetValue(controller, xeno);
                button.UpdateBackground();
                Assert.That(button.Button.Texture, Is.SameAs(custom),
                    "targeting must use the preserved CM action background");

                selecting.SetValue(controller, null);
                button.UpdateBackground();
                Assert.That(button.Button.Texture, Is.SameAs(theme));

                var xenoAction = actions.GetAction(xeno)!.Value;
                xenoAction.Comp.Toggled = true;
                button.UpdateData(xeno, actions);
                Assert.That(button.Button.Texture, Is.SameAs(custom),
                    "toggled actions must use the preserved CM action background");

                xenoAction.Comp.Toggled = false;
                button.UpdateData(vehicle, actions);
                selecting.SetValue(controller, vehicle);
                button.UpdateBackground();
                Assert.That(button.Button.Texture, Is.SameAs(theme),
                    "rebinding must clear the previous action's custom background cache");
                selecting.SetValue(controller, null);

                AssertItemIconStyle(button, actions, vehicle, ItemActionIconStyle.BigAction);
                AssertItemIconStyle(button, actions, lockAction, ItemActionIconStyle.BigItem);
                AssertItemIconStyle(button, actions, wag, ItemActionIconStyle.NoItem);
            }
            finally
            {
                selecting.SetValue(controller, null);
                button.Dispose();
            }
        });

        await Server.WaitPost(() => SEntMan.RemoveComponent<ActionComponent>(dynamic));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = Client.System<SpriteSystem>();
            var dynamicAction = ClientEntity("CMActionToggleAttachable");
            var vehicle = ClientEntity("ActionVehicleToggleView");
            Assert.Multiple(() =>
            {
                Assert.That(CEntMan.HasComponent<ActionComponent>(dynamicAction), Is.False,
                    "the canonical action shutdown must remove the replicated component");
                Assert.That(spriteSystem.LayerMapTryGet(
                    dynamicAction,
                    ActionVisuals.IconToggled,
                    out _,
                    false), Is.False,
                    "post-shutdown visual cleanup must remove the dynamic toggled layer map");
                Assert.That(spriteSystem.LayerMapTryGet(
                    vehicle,
                    ActionVisuals.IconToggled,
                    out var layer,
                    false), Is.True,
                    "shutting down another action must not remove a prototype-defined toggled layer");
                Assert.That(layer, Is.EqualTo(initialVehicleToggledLayer));
            });
        });

        await Server.WaitPost(() =>
        {
            Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            foreach (var uid in serverEntities.Values)
                SEntMan.DeleteEntity(uid);
        });

        EntityUid ClientEntity(string prototype)
        {
            return CEntMan.GetEntity(netEntities[prototype]);
        }
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                foreach (var uid in serverEntities.Values)
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private void AssertBackground(string id, string state)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<ActionComponent>(out var action, SEntMan.ComponentFactory), Is.True, id);
        Assert.That(action!.BackgroundOn, Is.EqualTo(new SpriteSpecifier.Rsi(
            new ResPath("_RMC14/Actions/xeno_actions.rsi"),
            state)), id);
    }

    private void AssertStyle(string id, ItemActionIconStyle style)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<ActionComponent>(out var action, SEntMan.ComponentFactory), Is.True, id);
        Assert.That(action!.ItemIconStyle, Is.EqualTo(style), id);
    }

    private ISpriteLayer GetLayer(
        SpriteSystem spriteSystem,
        EntityUid uid,
        ActionVisuals key)
    {
        return GetLayer(spriteSystem, uid, key, out _);
    }

    private ISpriteLayer GetLayer(
        SpriteSystem spriteSystem,
        EntityUid uid,
        ActionVisuals key,
        out int index)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
        Assert.That(spriteSystem.LayerMapTryGet((uid, sprite), key, out index, false), Is.True, key.ToString());
        return sprite[index];
    }

    private void AssertRsiLayer(
        SpriteSystem spriteSystem,
        EntityUid uid,
        ActionVisuals key,
        string rsi,
        string? state,
        bool visible,
        Color? color = null)
    {
        var layer = GetLayer(spriteSystem, uid, key);
        Assert.Multiple(() =>
        {
            Assert.That(layer.ActualRsi?.Path, Is.EqualTo(new ResPath(rsi)), key.ToString());
            if (state != null)
                Assert.That(layer.RsiState.Name, Is.EqualTo(state), key.ToString());
            Assert.That(layer.Visible, Is.EqualTo(visible), key.ToString());
            if (color != null)
                Assert.That(layer.Color, Is.EqualTo(color.Value), key.ToString());
        });
    }

    private void AssertTextureLayer(
        SpriteSystem spriteSystem,
        EntityUid uid,
        ActionVisuals key,
        string texture,
        bool visible,
        Color? color = null,
        int? expectedLayer = null)
    {
        var layer = GetLayer(spriteSystem, uid, key, out var index);
        Assert.Multiple(() =>
        {
            Assert.That(layer.Texture, Is.SameAs(spriteSystem.Frame0(
                new SpriteSpecifier.Texture(new ResPath(texture)))), key.ToString());
            Assert.That(layer.Visible, Is.EqualTo(visible), key.ToString());
            if (color != null)
                Assert.That(layer.Color, Is.EqualTo(color.Value), key.ToString());
            if (expectedLayer != null)
                Assert.That(index, Is.EqualTo(expectedLayer.Value), key.ToString());
        });
    }

    private static void AssertItemIconStyle(
        ActionButton button,
        ClientActionsSystem actions,
        EntityUid action,
        ItemActionIconStyle style)
    {
        var entity = actions.GetAction(action)!.Value;
        entity.Comp.EntIcon = action;
        button.UpdateData(action, actions);

        var bigAction = GetPrivate<SpriteView>(button, "_bigActionIcon");
        var smallAction = GetPrivate<SpriteView>(button, "_smallActionIcon");
        var bigItem = GetPrivate<SpriteView>(button, "_bigItemSpriteView");
        var smallItem = GetPrivate<SpriteView>(button, "_smallItemSpriteView");
        Assert.Multiple(() =>
        {
            Assert.That(entity.Comp.ItemIconStyle, Is.EqualTo(style));
            Assert.That(bigAction.Visible, Is.EqualTo(style != ItemActionIconStyle.BigItem));
            Assert.That(smallAction.Visible, Is.EqualTo(style == ItemActionIconStyle.BigItem));
            Assert.That(bigItem.Visible, Is.EqualTo(style == ItemActionIconStyle.BigItem));
            Assert.That(smallItem.Visible, Is.EqualTo(style == ItemActionIconStyle.BigAction));
        });
        entity.Comp.EntIcon = null;
    }

    private static T GetPrivate<T>(object instance, string name)
    {
        return (T) instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }
}

#pragma warning restore RA0002
