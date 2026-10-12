using Content.IntegrationTests.Fixtures;
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
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            foreach (var slotId in mount.GetAmmoSlots())
            {
                var crate = SEntMan.SpawnEntity("CMUMohawkM90Ammo",
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
            Assert.That(window.FeedStatusLabel.Text, Does.Contain("400 rounds"));
            Assert.That(console.ChinGunFeedOne, Does.Contain("400 rounds"));
            Assert.That(console.ChinGunFeedTwo, Does.Contain("400 rounds"));
            Assert.That(window.FreeFireButton.Visible, Is.True);
            Assert.That(window.ChinGunHealthLabel.Text, Does.Contain("2000"));
            var bui = (DropshipWeaponsBui) CEntMan.GetComponent<UserInterfaceComponent>(CEntMan.GetEntity(consoleNet))
                .ClientOpenInterfaces[DropshipTerminalWeaponsUi.Key];
            var preview = window.ScreenOne.Viewport.Children.OfType<DropshipTargetingPreview>().Single();
            var elapsed = typeof(DropshipTargetingPreview).GetField("_aimElapsed", BindingFlags.Instance | BindingFlags.NonPublic)!;
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
                    Assert.That(viewport.Children.OfType<DropshipTargetingPreview>().Single(), Is.SameAs(preview));
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
    }
}
