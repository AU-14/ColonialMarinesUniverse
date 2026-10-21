using Content.IntegrationTests.Fixtures;
using Content.Client.CMU14.Dropship.DirectFire;
using Content.Client._RMC14.Dropship.Weapon;
using Content.Client.UserInterface.ControlExtensions;
using Robust.Client.UserInterface;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.Eye;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using System.Reflection;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkMappedGunneryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("midway")]
    [TestCase("midway_navy")]
    public async Task MappedUpperDeckGunnerCanControlLowerDeckTurret(string variant)
    {
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<Robust.Shared.Configuration.IConfigurationManager>()
                .SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            var maps = Server.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(Server.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out _), Is.True);
        });
        await Pair.RunSeconds(1);
        NetEntity consoleNet = default;
        NetEntity actorNet = default;
        await Server.WaitAssertion(() =>
        {
            var point = SEntMan.QueryEntities<MohawkChinGunComponent>().Single();
            point.Comp.Calibrated = true;
            point.Comp.IffConfigured = true;
            Server.System<Content.Shared._RMC14.Sentry.SharedSentryTargetingSystem>().SetFriendlyFactions(
                (point.Owner, SEntMan.GetComponent<Content.Shared._RMC14.Sentry.SentryTargetingComponent>(point)), ["GOVFOR"]);
            Assert.That(Server.System<SharedDropshipSystem>().TryGetGridDropship(point, out var canonicalShip), Is.True);
            SEntMan.EnsureComponent<DropshipFireControlBootComponent>(canonicalShip).Complete = true;
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            foreach (var slotId in mount.GetAmmoSlots())
            {
                var crate = SEntMan.SpawnEntity("CMUMohawkM90AmmoKinetic",
                    SEntMan.GetComponent<TransformComponent>(point).Coordinates);
                Assert.That(containers.Insert(crate, containers.GetContainer(point, slotId)), Is.True);
            }
            var seat = SEntMan.QueryEntities<MohawkGunnerySeatComponent>().Single();
            var console = SEntMan.QueryEntities<DropshipTerminalWeaponsComponent>()
                .Single(ent => ent.Comp.Gunnery);
            var actor = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetComponent<TransformComponent>(seat).Coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
            Server.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 1);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(actor, null, seat, popup: false), Is.True);
            var ui = Server.System<SharedUserInterfaceSystem>();
            ui.OpenUi(console.Owner, DropshipTerminalWeaponsUi.Key, actor);
            Assert.That(ui.IsUiOpen(console.Owner, DropshipTerminalWeaponsUi.Key, actor), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new MohawkChinGunControlMessage
            {
                Actor = actor,
                UiKey = DropshipTerminalWeaponsUi.Key,
            });
            Assert.That(point.Comp.Operator, Is.EqualTo(actor));
            Assert.That(SEntMan.GetComponent<EyeComponent>(point.Comp.Eye!.Value).DrawFov, Is.True,
                "Landed gun cameras use wall occlusion.");
            Assert.That(Server.System<MohawkChinGunSystem>().CanOperate(point, actor,
                SEntMan.GetComponent<RemoteWeaponOperatorComponent>(actor).SelectedWeapon!.Value, out _), Is.True);
            consoleNet = SEntMan.GetNetEntity(console);
            actorNet = SEntMan.GetNetEntity(actor);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var console = CEntMan.GetComponent<DropshipTerminalWeaponsComponent>(CEntMan.GetEntity(consoleNet));
            Assert.That(console.ChinGunOperator, Is.EqualTo(CEntMan.GetEntity(actorNet)));
            Assert.That(console.ChinGunEye, Is.Not.Null);
            Assert.That(Client.ResolveDependency<Robust.Client.Graphics.IOverlayManager>()
                .HasOverlay<Content.Client.CMU14.Dropship.DirectFire.MohawkGunnerOverlay>(), Is.True);
            var window = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<DropshipWeaponsWindow>(fullTreeSearch: true).Single();
            Assert.That(window.ScreenOne.Viewport.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Stop),
                "The embedded camera must receive clicks instead of silently ignoring target marks.");
            Assert.That(window.ConfirmTrackingButton.Visible, Is.True);
            Assert.That(window.ConfirmTrackingButton.Disabled, Is.True,
                "Authorization must be discoverable but unavailable before a hostile is marked.");
            Assert.That(window.AcquireTargetButton.Visible, Is.True);
            Assert.That(window.PriorityButton.Visible, Is.True);
            Assert.That(window.FeedModeButton.Visible, Is.True);
            Assert.That(window.FeedOneButton.Visible, Is.True);
            Assert.That(window.FeedTwoButton.Visible, Is.True);
            Assert.That(window.FeedStatusLabel.Text, Does.Contain("2400 rounds"));
            Assert.That(console.ChinGunFeedOne, Does.Contain("2400 rounds"));
            Assert.That(console.ChinGunFeedTwo, Does.Contain("2400 rounds"));
            Assert.That(window.FreeFireButton.Visible, Is.True);
            Assert.That(window.ChinGunHealthLabel.Text, Does.Contain("2000"));
            Assert.That(window.Panel.HasStyleClass("CrtPanel"), Is.True);
            Assert.That(window.TrackTargetButton.Text, Is.EqualTo("Mark target"));
            Assert.That(window.ScreenOne.TopRow.One.StyleBoxOverride, Is.Null,
                "The screen controls must use digital stylesheet states, not hard-coded legacy green borders.");
            var bui = (DropshipWeaponsBui) CEntMan.GetComponent<UserInterfaceComponent>(CEntMan.GetEntity(consoleNet))
                .ClientOpenInterfaces[DropshipTerminalWeaponsUi.Key];
            var preview = window.ScreenOne.Viewport.Children.OfType<MohawkGunnerPreview>().Single();
            var elapsed = typeof(MohawkGunnerPreview).GetField("_aimElapsed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            elapsed.SetValue(preview, 0.08f);
            var input = Client.ResolveDependency<IInputManager>();
            var enabled = input.Enabled;
            input.Enabled = true;
            try
            {
                input.KeyDown(new KeyEventArgs(Keyboard.Key.MouseLeft, false, false, false, false, false, 0));
                Assert.That(input.IsKeyDown(Keyboard.Key.MouseLeft), Is.True);
                var viewport = window.ScreenOne.Viewport;
                viewport.GetType().GetMethod("KeyBindDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(viewport, new object[] { new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down,
                        default, true, Vector2.Zero, Vector2.Zero) });
                var held = typeof(DropshipWeaponsBui).GetField("_manualTriggerHeld", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.That(held.GetValue(bui), Is.True);
                for (var i = 0; i < 100; i++)
                {
                    bui.Refresh();
                    Assert.That(viewport.Visible, Is.True);
                    Assert.That(viewport.Children.OfType<MohawkGunnerPreview>().Single(), Is.SameAs(preview));
                    Assert.That(elapsed.GetValue(preview), Is.EqualTo(0.08f),
                        "Ammo/aim refreshes must not starve the 100ms held-trigger heartbeat.");
                    Assert.That(held.GetValue(bui), Is.True);
                }
                viewport.GetType().GetMethod("KeyBindUp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(viewport, new object[] { new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up,
                        default, true, Vector2.Zero, Vector2.Zero) });
                Assert.That(held.GetValue(bui), Is.False, "Real UI release still stops the held trigger.");
            }
            finally
            {
                input.KeyUp(new KeyEventArgs(Keyboard.Key.MouseLeft, false, false, false, false, false, 0));
                input.Enabled = enabled;
            }
        });
        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.GetEntity(consoleNet);
            var actor = SEntMan.GetEntity(actorNet);
            SEntMan.EventBus.RaiseLocalEvent(console, new MohawkChinGunFeedMessage(1)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(SEntMan.QueryEntities<MohawkChinGunComponent>().Single().Comp.ActiveFeed, Is.EqualTo(1));
            SEntMan.EventBus.RaiseLocalEvent(console, new MohawkChinGunFeedMessage()
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var console = CEntMan.GetComponent<DropshipTerminalWeaponsComponent>(CEntMan.GetEntity(consoleNet));
            Assert.That(console.ChinGunAlternateFeeds, Is.True);
            var window = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<DropshipWeaponsWindow>(fullTreeSearch: true).Single();
            Assert.That(window.FeedModeButton.Text, Does.Contain("alternate"));
            Assert.That(window.FeedTwoButton.Text, Does.Contain("*"));
        });
        await Server.WaitAssertion(() =>
        {
            var point = SEntMan.QueryEntities<MohawkChinGunComponent>().Single();
            var actor = SEntMan.GetEntity(actorNet);
            SEntMan.EventBus.RaiseLocalEvent(SEntMan.GetEntity(consoleNet), new MohawkChinGunArMessage
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(point.Comp.ArMode, Is.True);
            Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(
                SEntMan.GetEntity(consoleNet), DropshipTerminalWeaponsUi.Key, actor), Is.True,
                "AR hides rather than closes the authoritative control session.");
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            if (ui.ActiveScreen is not Content.Client.UserInterface.Screens.DefaultGameScreen)
            {
                ui.UnloadScreen();
                ui.LoadScreen<Content.Client.UserInterface.Screens.DefaultGameScreen>();
            }
            var window = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<DropshipWeaponsWindow>(fullTreeSearch: true).Single();
            Assert.That(window.Visible, Is.False, "AR must hide the entire console panel.");
            var overlay = Client.ResolveDependency<Robust.Client.Graphics.IOverlayManager>()
                .GetOverlay<Content.Client.CMU14.Dropship.DirectFire.MohawkGunnerOverlay>();
            var mainViewport = ui.RootControl.GetControlOfType<Content.Client.UserInterface.Controls.MainViewport>(
                fullTreeSearch: true).Single();
            Assert.That(overlay.ShouldDrawForViewport(mainViewport.Viewport), Is.True,
                "The actual game ScalingViewport must draw the AR firing radius.");
            Assert.That(overlay.ShouldDrawForViewport(window.ScreenOne.Viewport), Is.False,
                "AR geometry must not leak into embedded targeting monitors.");
            Assert.That(Client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity,
                Is.EqualTo(CEntMan.GetEntity(actorNet)));
            var system = Client.System<Content.Client.CMU14.Dropship.DirectFire.MohawkGunnerInputSystem>();
            system.FrameUpdate(0.1f);
            var exit = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<Robust.Client.UserInterface.Controls.Button>(fullTreeSearch: true)
                .Single(button => button.Text == "Return to gun panel");
            Assert.That(exit.Visible, Is.True);
        });
        var enabledBeforePan = false;
        await Client.WaitPost(() =>
        {
            var input = Client.ResolveDependency<IInputManager>();
            enabledBeforePan = input.Enabled;
            input.Enabled = true;
            Client.ResolveDependency<IUserInterfaceManager>().KeyboardFocused?.ReleaseKeyboardFocus();
            var timing = Client.ResolveDependency<Robust.Shared.Timing.IGameTiming>();
            var message = new ClientFullInputCmdMessage(timing.CurTick, timing.TickFraction,
                input.NetworkBindMap.KeyFunctionID(EngineKeyFunctions.MoveRight)) { State = BoundKeyState.Down };
            Client.System<Robust.Client.GameObjects.InputSystem>().HandleInputCommand(
                Client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalSession,
                EngineKeyFunctions.MoveRight, message);
        });
        try
        {
            await Pair.RunTicksSync(4);
            await Client.WaitPost(() => Client.System<Content.Client.CMU14.Dropship.DirectFire.MohawkGunnerInputSystem>().FrameUpdate(0.1f));
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                var point = SEntMan.QueryEntities<MohawkChinGunComponent>().Single();
                Assert.That(point.Comp.CameraPan.X, Is.GreaterThan(0), "The bound MoveRight input must pan the camera right.");
                Assert.That(point.Comp.DesiredAimDegrees, Is.LessThan(0), "Keyboard panning must also slew the gun.");
            });
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                var input = Client.ResolveDependency<IInputManager>();
                var timing = Client.ResolveDependency<Robust.Shared.Timing.IGameTiming>();
                var message = new ClientFullInputCmdMessage(timing.CurTick, timing.TickFraction,
                    input.NetworkBindMap.KeyFunctionID(EngineKeyFunctions.MoveRight)) { State = BoundKeyState.Up };
                Client.System<Robust.Client.GameObjects.InputSystem>().HandleInputCommand(
                    Client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalSession,
                    EngineKeyFunctions.MoveRight, message);
                input.Enabled = enabledBeforePan;
            });
        }
        await Client.WaitPost(() =>
        {
            var exit = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<Robust.Client.UserInterface.Controls.Button>(fullTreeSearch: true)
                .Single(button => button.Text == "Return to gun panel");
            var pressed = false;
            exit.OnPressed += _ => pressed = true;
            exit.GetType().GetMethod("KeyBindDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(exit, new object[] { new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down,
                    default, true, Vector2.Zero, Vector2.Zero) });
            Assert.That(pressed, Is.True, "The AR return button must emit its actual press event.");
        });
        await Pair.RunTicksSync(3);
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.QueryEntities<MohawkChinGunComponent>().Single().Comp.ArMode, Is.False,
                "The networked AR exit must be accepted by the authoritative control session.");
            Assert.That(SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(SEntMan.GetEntity(consoleNet)).ChinGunAr, Is.False);
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<DropshipTerminalWeaponsComponent>(CEntMan.GetEntity(consoleNet)).ChinGunAr,
                Is.False, "The authoritative AR state must reach the client.");
            var window = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<DropshipWeaponsWindow>(fullTreeSearch: true).Single();
            Assert.That(window.Visible, Is.True);
        });
        await Server.WaitAssertion(() =>
        {
            var point = SEntMan.QueryEntities<MohawkChinGunComponent>().Single();
            Assert.That(point.Comp.ArMode, Is.False);
            Assert.That(point.Comp.Operator, Is.EqualTo(SEntMan.GetEntity(actorNet)),
                "Returning from AR must preserve turret control.");
            var previousEye = point.Comp.PreviousEye;
            Server.System<SharedUserInterfaceSystem>().CloseUi(
                SEntMan.GetEntity(consoleNet), DropshipTerminalWeaponsUi.Key, SEntMan.GetEntity(actorNet));
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(point.Comp.Operator, Is.Null);
            Assert.That(point.Comp.KeyboardPan, Is.EqualTo(Vector2.Zero));
            Assert.That(SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(SEntMan.GetEntity(consoleNet)).ChinGunAr, Is.False);
            Assert.That(SEntMan.GetComponent<EyeComponent>(SEntMan.GetEntity(actorNet)).Target, Is.EqualTo(previousEye),
                "Closing turret control must restore the gunner's original camera.");
        });
    }
}
