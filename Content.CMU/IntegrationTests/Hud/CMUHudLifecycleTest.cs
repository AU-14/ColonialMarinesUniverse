#pragma warning disable RA0002 // Supply a controlled hand order to exercise the real container lifecycle.
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Alert;
using Content.Shared.Hands.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Hud;

[TestFixture]
public sealed class CMUHudLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task HandEnumerationFollowsDisplayOrderThroughRemovalAndReuse()
    {
        await Client.WaitAssertion(() =>
        {
            using var hands = new HandsContainer
            {
                PlayerHandsComponent = new HandsComponent { SortedHands = ["middle", "left", "right"] },
            };
            using var right = new HandButton("right", HandLocation.Right);
            using var left = new HandButton("left", HandLocation.Left);
            using var middle = new HandButton("middle", HandLocation.Middle);
            hands.TryAddButton(right);
            hands.TryAddButton(left);
            hands.TryAddButton(middle);

            void Check(params string[] expected)
            {
                var direct = new List<string>();
                foreach (var hand in hands.GetButtons())
                    direct.Add(hand.SlotName);
                Assert.That(direct, Is.EqualTo(expected));
                IEnumerable<HandButton> compatible = hands.GetButtons();
                Assert.That(compatible.Select(hand => hand.SlotName), Is.EqualTo(expected));
            }

            Check("middle", "left", "right");
            right.SetPositionInParent(0);
            Check("right", "middle", "left");
            hands.TryRemoveButton("middle", out _);
            Check("right", "left");
            hands.ClearButtons();
            Check();
            hands.TryAddButton(middle);
            Check("middle");
        });
    }

    [Test]
    public async Task AlertCreatesOneIconPerMountAndReleasesItOnExit()
    {
        EntityUid first = default;
        EntityUid second = default;
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var sprites = Client.System<SpriteSystem>();
            ProtoId<AlertPrototype> id = "AreaInfo";
            var prototype = CProtoMan.Index(id);
            var before = CEntMan.EntityCount;
            using var alert = new AlertControl(prototype, prototype.MinSeverity);
            Assert.That(CEntMan.EntityCount, Is.EqualTo(before), "An unmounted alert needs no sprite entity.");
            var view = alert.Children.OfType<SpriteView>().Single();

            void CheckSeverity(short severity)
            {
                var uid = view.Entity!.Value;
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                Assert.That(sprites.LayerMapTryGet((uid, sprite), AlertVisualLayers.Base, out var layer, false), Is.True);
                var icon = (SpriteSpecifier.Rsi) prototype.GetIcon(severity);
                Assert.That(sprites.LayerGetRsiState((uid, sprite), layer).ToString(), Is.EqualTo(icon.RsiState));
            }

            alert.SetSeverity(prototype.MaxSeverity);
            ui.WindowRoot.AddChild(alert);
            first = view.Entity!.Value;
            Assert.That(CEntMan.EntityCount, Is.EqualTo(before + 1));
            CheckSeverity(prototype.MaxSeverity);
            alert.SetSeverity(prototype.MinSeverity);
            CheckSeverity(prototype.MinSeverity);

            alert.Orphan();
            Assert.That(CEntMan.IsQueuedForDeletion(first), Is.True);
            alert.SetSeverity(prototype.MaxSeverity);
            ui.WindowRoot.AddChild(alert);
            second = view.Entity!.Value;
            Assert.That(second, Is.Not.EqualTo(first), "Re-entry before deferred deletion must create a live replacement.");
            Assert.That(CEntMan.EntityCount, Is.EqualTo(before + 2));
            CheckSeverity(prototype.MaxSeverity);
            alert.Orphan();
            Assert.That(CEntMan.IsQueuedForDeletion(second), Is.True);
        });

        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.Deleted(first), Is.True);
            Assert.That(CEntMan.Deleted(second), Is.True);
        });
    }
}
