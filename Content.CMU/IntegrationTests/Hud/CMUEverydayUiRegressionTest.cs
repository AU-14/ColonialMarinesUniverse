using System.Numerics;
using System.Reflection;
using System.Threading;
using Content.Client.ContextMenu.UI;
using Content.Client.Examine;
using Content.Client.Verbs.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Hud;

[TestFixture]
public sealed class CMUEverydayUiRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [Test]
    public async Task NestedMenusReleaseTheirStackAndCallbacksAcrossStateChanges()
    {
        await Client.WaitAssertion(() =>
        {
            var context = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ContextMenuUIController>();
            context.Setup();
            var root = context.RootMenu;
            var row = new ContextMenuElement("Parent");
            context.AddElement(root, row);
            var submenu = new ContextMenuPopup(context, row);
            var child = new ContextMenuElement("Child");
            context.AddElement(submenu, child);
            var nested = new ContextMenuPopup(context, child);
            context.AddElement(nested, new ContextMenuElement("Leaf"));
            root.Open(UIBox2.FromDimensions(Vector2.Zero, Vector2.One));
            context.OpenSubMenu(row);
            context.OpenSubMenu(child);
            Assert.That(context.Menus.Count, Is.EqualTo(3));

            var closed = 0;
            void OnClose() { closed++; context.Close(); }
            context.OnContextClosed += OnClose;
            context.CancelOpen = new CancellationTokenSource();
            var pending = context.CancelOpen.Token;
            context.Close();
            Assert.That(closed, Is.EqualTo(1), "Hiding the root and closing callbacks must not reenter teardown.");
            Assert.That(pending.IsCancellationRequested, Is.True);
            Assert.That(context.CancelOpen, Is.Null);
            Assert.That(context.Menus, Is.EqualTo(new[] { root }));
            Assert.That(submenu.Parent, Is.Null);
            Assert.That(nested.Parent, Is.Null);
            context.OpenSubMenu(row);
            Assert.That(context.Menus, Is.EqualTo(new[] { root }), "A delayed hover cannot reopen removed rows.");
            context.OnContextClosed -= OnClose;

            var keys = 0;
            void OnKey(ContextMenuElement _, GUIBoundKeyEventArgs __) => keys++;
            context.OnContextKeyEvent += OnKey;
            context.AddElement(root, row);
            var key = row.GetType().GetMethod("KeyBindDown", Fields)!.CreateDelegate<Action<GUIBoundKeyEventArgs>>(row);
            key(UseKey());
            Assert.That(keys, Is.EqualTo(1));
            row.Orphan();
            key(UseKey());
            Assert.That(keys, Is.EqualTo(1), "Detached rows must not call the controller.");
            context.OnContextKeyEvent -= OnKey;

            for (var i = 0; i < 3; i++)
            {
                var previousRoot = context.RootMenu;
                context.Shutdown();
                Assert.That(context.Menus, Is.Empty);
                Assert.That(previousRoot.Parent, Is.Null);
                context.Setup();
                Assert.That(context.Menus, Is.EqualTo(new[] { context.RootMenu }));
            }
        });
    }

    [Test]
    public async Task VerbRepliesReuseRowsAndRefreshLazyCategoriesAndSingleActionShortcuts()
    {
        var (targetNet, _) = await CreatePeople();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var context = ui.GetUIController<ContextMenuUIController>();
            context.Setup();
            var menu = context.RootMenu;
            var verbs = ui.GetUIController<VerbMenuUIController>();
            verbs.CurrentTarget = targetNet;
            var actions = 0;
            var alpha = new Verb { Text = "Alpha", ClientExclusive = true };
            var bravo = new Verb { Text = "Bravo", Category = VerbCategory.Examine };
            var charlie = new Verb { Text = "Charlie", Category = VerbCategory.Examine };
            var single = new Verb { Text = "Rotate", Category = VerbCategory.Rotate, ClientExclusive = true,
                CloseMenu = false, Act = () => actions++ };
            verbs.CurrentVerbs = new SortedSet<Verb> { alpha, bravo, charlie, single };
            verbs.ExtraCategories.Clear();
            verbs.AddServerVerbs([], menu);
            menu.Open(UIBox2.FromDimensions(Vector2.Zero, Vector2.One));
            var alphaRow = Row(menu, alpha);
            var category = Category(menu, VerbCategory.Examine);
            Assert.That(category.SubMenu!.MenuBody.ChildCount, Is.Zero);
            var rotate = Category(menu, VerbCategory.Rotate);
            Assert.That(rotate.SubMenu!.MenuBody.ChildCount, Is.Zero);
            verbs.OnKeyBindDown(rotate, UseKey());
            Assert.That(actions, Is.EqualTo(1), "A single lazy category action must execute on the first click.");

            var added = new Verb { Text = "Delta" };
            verbs.AddServerVerbs([added], menu);
            Assert.That(Row(menu, alpha), Is.SameAs(alphaRow));
            Assert.That(Category(menu, VerbCategory.Examine), Is.SameAs(category));
            Assert.That(menu.MenuBody.Children.OfType<VerbMenuElement>().Where(e => e.Verb != null).Select(e => e.Verb),
                Is.EqualTo(new[] { alpha, added }));
            context.OpenSubMenu(category);
            var bravoRow = Row(category.SubMenu!, bravo);
            var charlieRow = Row(category.SubMenu!, charlie);
            bravo.Disabled = true;
            bravo.Message = "Unavailable";
            verbs.AddServerVerbs([], menu);
            Assert.That(Row(category.SubMenu!, bravo), Is.Not.SameAs(bravoRow));
            Assert.That(Row(category.SubMenu!, bravo).Disabled, Is.True);
            Assert.That(Row(category.SubMenu!, charlie), Is.SameAs(charlieRow));
            Assert.That(category.SubMenu!.Visible, Is.True);

            verbs.AddServerVerbs(null, menu);
            Assert.That(menu.MenuBody.Children.OfType<VerbMenuElement>(), Is.Empty);
            Assert.That(menu.MenuBody.ChildCount, Is.EqualTo(1));
            Assert.That(context.Menus, Is.EqualTo(new[] { menu }));
            context.Close();
        });
    }

    [Test]
    public async Task ExamineRefreshReusesPopupButReplacesTextIdentityAndVerbsAndChecksLiveRange()
    {
        var (firstNet, secondNet) = await CreatePeople();
        await Client.WaitAssertion(() =>
        {
            var examine = Client.System<ExamineSystem>();
            var player = Client.Session!.AttachedEntity!.Value;
            var first = CEntMan.GetEntity(firstNet);
            var second = CEntMan.GetEntity(secondNet);
            var examiner = CEntMan.GetComponent<ExaminerComponent>(player);
            examiner.SkipChecks = true;
            Client.System<MetaDataSystem>().SetEntityName(Identity.Entity(first, CEntMan, player),
                "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW");
            examine.SendExamineTooltip(player, first, FormattedMessage.FromMarkupOrThrow("first message"), false, true);
            var popup = Get<Popup>(examine, "_examineTooltipOpen");
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var updateLayout = ui.GetType().GetMethod("FrameUpdate")!.CreateDelegate<Action<FrameEventArgs>>(ui);
            var availableSize = new Vector2(800, 600);
            updateLayout(new FrameEventArgs(0));
            popup.Measure(availableSize);
            var wideHeaderSize = popup.DesiredSize;
            var body = Get<RichTextLabel>(examine, "_cmuBodyLabel");
            var header = Get<RichTextLabel>(examine, "_cmuNameLabel");
            examine.SendExamineTooltip(player, first, FormattedMessage.FromMarkupOrThrow("server replacement"), false, true);
            Assert.That(Get<Popup>(examine, "_examineTooltipOpen"), Is.SameAs(popup));
            Assert.That(Get<RichTextLabel>(examine, "_cmuBodyLabel"), Is.SameAs(body));
            Assert.That(body.GetMessage(), Is.EqualTo("server replacement"));
            examine.OpenTooltip(player, first, knowTarget: false);
            Assert.That(header.GetMessage(), Does.Contain("???"));
            // Label measurement invalidates ancestors through the UI's queued layout pass.
            updateLayout(new FrameEventArgs(0));
            popup.Measure(availableSize);
            Assert.That(popup.DesiredSize.X, Is.LessThan(wideHeaderSize.X),
                "A reused popup must shrink when a wide known header becomes unknown.");
            Client.System<MetaDataSystem>().SetEntityName(Identity.Entity(first, CEntMan, player), "Changed identity");
            examine.OpenTooltip(player, first, knowTarget: true);
            Assert.That(header.GetMessage(), Does.Contain("Changed identity"));

            var oldVerb = new ExamineVerb
            {
                Text = "Old",
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),
            };
            examine.UpdateTooltipInfo(player, first, FormattedMessage.FromMarkupOrThrow("with button"), [oldVerb], false);
            var buttons = Get<BoxContainer>(examine, "_cmuClickButtons");
            Assert.That(buttons.Children.OfType<ExamineButton>().Select(b => b.Verb), Does.Contain(oldVerb));
            var replacement = new ExamineVerb { Text = "New", Icon = oldVerb.Icon, Disabled = true };
            examine.UpdateTooltipInfo(player, first, new FormattedMessage(), [replacement], false);
            Assert.That(body.Visible, Is.False);
            Assert.That(buttons.Children.OfType<ExamineButton>().Select(b => b.Verb), Does.Not.Contain(oldVerb).And.Contains(replacement));
            Assert.That(buttons.Children.OfType<ExamineButton>().Single(b => b.Verb == replacement).Disabled, Is.True);

            examine.OpenTooltip(player, second);
            Assert.That(Get<Popup>(examine, "_examineTooltipOpen"), Is.Not.SameAs(popup));
            Assert.That(popup.Parent, Is.Null);
            examine.Update(1f / 30);
            Assert.That(Get<Popup>(examine, "_examineTooltipOpen").Visible, Is.True);
            examiner.SkipChecks = false;
            examiner.CheckInRangeUnOccluded = true;
            Client.System<SharedTransformSystem>().SetWorldPosition(second, new Vector2(500, 500));
            examine.Update(1f / 30);
            Assert.That(Get<Popup?>(examine, "_examineTooltipOpen"), Is.Null);
            Assert.That(Get<RichTextLabel?>(examine, "_cmuBodyLabel"), Is.Null);
        });
    }

    private async Task<(NetEntity First, NetEntity Second)> CreatePeople()
    {
        var map = await Pair.CreateTestMap();
        NetEntity first = default;
        NetEntity second = default;
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            first = SEntMan.GetNetEntity(SEntMan.SpawnEntity("CMMobHuman", map.GridCoords));
            second = SEntMan.GetNetEntity(SEntMan.SpawnEntity("CMMobHuman", map.GridCoords));
        });
        await Pair.RunUntilSynced();
        return (first, second);
    }

    private static T Get<T>(object instance, string field) => (T) instance.GetType().GetField(field, Fields)!.GetValue(instance)!;
    private static VerbMenuElement Row(ContextMenuPopup menu, Verb verb) =>
        menu.MenuBody.Children.OfType<VerbMenuElement>().Single(row => row.Verb == verb);
    private static VerbMenuElement Category(ContextMenuPopup menu, VerbCategory category) =>
        menu.MenuBody.Children.OfType<VerbMenuElement>().Single(row => Get<VerbCategory?>(row, "CMUCategory") == category);
    private static GUIBoundKeyEventArgs UseKey() => new(EngineKeyFunctions.Use, BoundKeyState.Down, default, false, default, default);
}
