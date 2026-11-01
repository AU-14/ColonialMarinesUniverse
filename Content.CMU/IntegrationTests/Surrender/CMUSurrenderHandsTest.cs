using System.Linq;
using Content.Client.Gameplay;
using Content.Client.Resources;
using Content.Client.UserInterface.Systems.Hands;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderHandsTest : GameTest
{
    [TestCase(HandLocation.Left)]
    [TestCase(HandLocation.Right)]
    public async Task IndicatorsFollowTheLocalPlayerAndRestoreNativeHands(HandLocation removedSide)
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        var states = Client.ResolveDependency<IStateManager>();
        var originalState = states.CurrentState.GetType();
        var controller = ui.GetUIController<HandsUIController>();
        var cache = Client.ResolveDependency<IResourceCache>();
        EntityUid source = default;
        EntityUid other = default;
        EntityUid item = default;

        void CheckHands(bool raised, int count = 2)
        {
            controller.FrameUpdate(new FrameEventArgs(1f / 60f));
            var gui = ui.GetActiveUIWidgetOrNull<HotbarGui>();
            Assert.That(gui, Is.Not.Null);
            var buttons = gui!.HandContainer.GetButtons().ToArray();
            Assert.That(buttons, Has.Length.EqualTo(count));
            foreach (var button in buttons)
            {
                var side = button.HandLocation == HandLocation.Left ? "l" : "r";
                var path = $"/Textures/Interface/Default/Slots/hand_{side}.png";
                Assert.That(button.ButtonRect.Texture, Is.SameAs(cache.GetTexture(path)));
                var indicators = button.Children.OfType<TextureRect>()
                    .Where(child => child.Name == "CMUSurrenderIndicator").ToArray();
                Assert.That(indicators.Length, Is.LessThanOrEqualTo(1));
                var visible = raised && button.Entity == null && !button.Blocked;
                Assert.That(indicators.Any(child => child.Visible), Is.EqualTo(visible));
                if (indicators.SingleOrDefault() is { } indicator)
                {
                    Assert.That(indicator.Texture, Is.SameAs(Client.System<SpriteSystem>().Frame0(
                        new SpriteSpecifier.Rsi(new ResPath("_RMC14/Interface/xeno_interface.rsi"), "blocked"))));
                    Assert.That(indicator.Modulate.A, Is.EqualTo(0.65f).Within(0.001f));
                    Assert.That(indicator.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Ignore));
                }
            }
        }

        try
        {
            await Server.WaitPost(() =>
            {
                source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                item = SEntMan.SpawnEntity("Crowbar", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, source);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                states.RequestStateChange<GameplayState>();
                controller.ReloadHands();
                CheckHands(false);
            });

            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                other, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(false));

            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                source, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                CheckHands(true);
                var buttons = ui.GetActiveUIWidget<HotbarGui>().HandContainer.GetButtons().ToArray();
                var highlighted = buttons.Select(button => button.Highlight).ToArray();
                CheckHands(true);
                Assert.That(buttons.Select(button => button.Highlight), Is.EqualTo(highlighted));

                // A native blocker must remain visible instead of being replaced by the surrender artwork.
                buttons[0].Blocked = true;
                CheckHands(true);
                Assert.That(buttons[0].BlockedRect.Visible, Is.True);
                buttons[0].Blocked = false;
                CheckHands(true);
                controller.ReloadHands();
                CheckHands(true);
            });

            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, null));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(false, 0));
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, source));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(true));

            await Server.WaitPost(() => Server.System<CMUSurrenderSystem>().LowerHands(other));
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, other));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(false));
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, source));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(true));

            await Server.WaitAssertion(() => Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(
                source, item), Is.False));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(true));

            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                source, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(false));

            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                source, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(true));
            await Server.WaitPost(() =>
            {
                var hands = SEntMan.GetComponent<HandsComponent>(source);
                var name = hands.Hands.Single(pair => pair.Value.Location == removedSide).Key;
                Server.System<SharedHandsSystem>().RemoveHand((source, hands), name);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(true, 1));
            await Server.WaitPost(() => Server.System<CMUSurrenderSystem>().LowerHands(source));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => CheckHands(false, 1));
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => states.RequestStateChange(originalState));
            foreach (var entity in new[] { source, other, item })
            {
                if (SEntMan.EntityExists(entity))
                    await Pair.DeleteEntityTreeLeafFirst(entity);
            }
        }
    }
}
