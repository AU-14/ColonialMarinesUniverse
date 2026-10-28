#pragma warning disable RA0002 // Exercise real client UI transitions with controlled component state.
using System.Numerics;
using System.Reflection;
using Content.Client.Actions.UI;
using Content.Client.CMU14.Storage;
using Content.Client.ContextMenu.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Inventory;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Server.Hands.Systems;
using Content.Shared._RMC14.IconLabel;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Hud;

[TestFixture]
public sealed class CMUCommonUiRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMPillCanister
  id: CMUUiRegressionItem
  components:
  - type: Item
    size: Tiny
    shape:
    - 0,0,0,0

- type: entity
  parent: ClothingBackpack
  id: CMUUiRegressionStorage
  components:
  - type: Storage
    grid:
    - 0,0,7,7
    maxItemSize: Huge
""";

    [Test]
    public async Task InventoryHoverRefreshesSpriteAndReleasesPreviewOnExitAndDispose()
    {
        var scenario = await CreateInventory();
        var previews = new List<EntityUid>();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var inventory = ui.GetUIController<InventoryUIController>();
            Set(inventory, "_playerUid", Client.Session!.AttachedEntity);
            using var button = new SlotButton { SlotName = "back" };
            ui.WindowRoot.AddChild(button);
            button.MouseIsHovering = true;
            inventory.UpdateHover(button);
            var first = button.HoverSpriteView.Entity!.Value;
            previews.Add(first);
            var count = CEntMan.EntityCount;
            for (var i = 0; i < 100; i++)
                inventory.UpdateHover(button);
            Assert.That(CEntMan.EntityCount, Is.EqualTo(count), "Refreshes must not strand previous previews.");

            var held = CEntMan.GetEntity(scenario.Held);
            var rotation = Angle.FromDegrees(37);
            Client.System<SpriteSystem>().SetRotation(held, rotation);
            inventory.UpdateHover(button);
            Assert.That(CEntMan.GetComponent<SpriteComponent>(button.HoverSpriteView.Entity!.Value).Rotation, Is.EqualTo(rotation));

            button.Orphan();
            Assert.That(button.HoverSpriteView.Entity, Is.Null);
            Assert.That(CEntMan.IsQueuedForDeletion(first), Is.True);
            Assert.That(button.Disposed, Is.False);
            ui.WindowRoot.AddChild(button);
            button.MouseIsHovering = true;
            inventory.UpdateHover(button);
            var second = button.HoverSpriteView.Entity!.Value;
            previews.Add(second);
            Assert.That(second, Is.Not.EqualTo(first), "Re-entering before deferred deletion needs a live preview.");
            button.ClearHover();
            Assert.That(CEntMan.IsQueuedForDeletion(second), Is.True);

            // Detached controls can also acquire a preview and must release it on disposal.
            button.Orphan();
            button.MouseIsHovering = true;
            inventory.UpdateHover(button);
            var detached = button.HoverSpriteView.Entity!.Value;
            previews.Add(detached);
            button.Dispose();
            Assert.That(CEntMan.IsQueuedForDeletion(detached), Is.True);
        });
        await Pair.RunTicksSync(3);
        await Client.WaitAssertion(() => Assert.That(previews.All(CEntMan.Deleted), Is.True));
    }

    [Test]
    public async Task TooltipUpdatesChangedTextAndCooldownAfterHiddenStates()
    {
        await Client.WaitAssertion(() =>
        {
            using var tooltip = new ActionAlertTooltip(FormattedMessage.FromUnformatted("test"), null);
            var frame = Frame(tooltip);
            var dynamic = Get<RichTextLabel>(tooltip, "_dynamicMessageLabel");
            var cooldown = Get<RichTextLabel>(tooltip, "_cooldownLabel");
            tooltip.DynamicMessage = "First";
            frame();
            Assert.That(dynamic.Text, Does.Contain("First"));
            tooltip.DynamicMessage = "Second";
            frame();
            Assert.That(dynamic.Text, Does.Contain("Second").And.Not.Contains("First"));
            tooltip.DynamicMessage = " ";
            frame();
            Assert.That(dynamic.Visible, Is.False);
            tooltip.DynamicMessage = "Second";
            frame();
            Assert.That(dynamic.Visible, Is.True);

            var now = CGameTiming.CurTime;
            tooltip.Cooldown = (now, now + TimeSpan.FromSeconds(1.25));
            frame();
            Assert.That(cooldown.Text, Is.EqualTo(CooldownText(1, 2)));
            tooltip.Cooldown = (now - TimeSpan.FromSeconds(1), now + TimeSpan.FromSeconds(0.25));
            frame();
            Assert.That(cooldown.Text, Is.EqualTo(CooldownText(1, 1)));
            tooltip.Cooldown = (now - TimeSpan.FromSeconds(2), now + TimeSpan.FromSeconds(0.25));
            frame();
            Assert.That(cooldown.Text, Is.EqualTo(CooldownText(2, 1)));
            tooltip.Cooldown = (now - TimeSpan.FromSeconds(1), now);
            frame();
            Assert.That(cooldown.Visible, Is.False);
            tooltip.Cooldown = (now - TimeSpan.FromSeconds(1), now + TimeSpan.FromSeconds(0.25));
            frame();
            Assert.That(cooldown.Visible, Is.True);
            Assert.That(cooldown.Text, Is.EqualTo(CooldownText(1, 1)));
            tooltip.Cooldown = null;
            frame();
            Assert.That(cooldown.Visible, Is.False);
        });
    }

    [Test]
    public async Task StorageRefreshMovesExistingPieceAndRechecksSavedSlots()
    {
        var scenario = await CreateInventory();
        await Client.WaitAssertion(() =>
        {
            var bag = CEntMan.GetEntity(scenario.Bag);
            var held = CEntMan.GetEntity(scenario.Held);
            var storage = CEntMan.GetComponent<StorageComponent>(bag);
            var moving = storage.StoredItems.Keys.First();
            var original = storage.StoredItems[moving];
            var destination = new ItemStorageLocation(Angle.Zero, new Vector2i(7, 7));
            using var window = new StorageWindow();
            window.UpdateContainer((bag, storage));
            window.Open();
            window.BuildItemPieces();
            var pieces = Get<Dictionary<EntityUid, (ItemStorageLocation?, ItemGridPiece)>>(window, "_pieces");
            var piece = pieces[moving].Item2;
            var originalParent = piece.Parent;
            storage.SavedLocations[CEntMan.GetComponent<MetaDataComponent>(held).EntityName] = [original, destination];
            var frame = Frame(window);
            frame();
            Assert.That(window.TryGetBackgroundCell(original.Position.X, original.Position.Y, out var oldCell), Is.True);
            Assert.That(window.TryGetBackgroundCell(7, 7, out var newCell), Is.True);
            var blocked = Color.FromHex("#2222CC");
            Assert.That(oldCell!.ModulateSelfOverride, Is.EqualTo(blocked));
            Assert.That(newCell!.ModulateSelfOverride, Is.Not.Null.And.Not.EqualTo(blocked));

            Assert.That(Client.System<SharedStorageSystem>().TrySetItemStorageLocation(moving, bag, destination), Is.True);
            window.BuildItemPieces();
            Assert.That(pieces[moving].Item2, Is.SameAs(piece));
            Assert.That(piece.Location, Is.EqualTo(destination));
            Assert.That(piece.Parent, Is.Not.SameAs(originalParent));
            frame();
            Assert.That(oldCell.ModulateSelfOverride, Is.Not.Null.And.Not.EqualTo(blocked));
            Assert.That(newCell.ModulateSelfOverride, Is.EqualTo(blocked), "Occupancy must reflect the move on the next frame.");
            storage.SavedLocations.Clear();
            frame();
            Assert.That(oldCell.ModulateSelfOverride, Is.Null);
            Assert.That(newCell.ModulateSelfOverride, Is.Null);
            window.Close();
        });
    }

    [Test]
    public async Task ContextMenuRechecksVisibilityAndRangeAfterGroupRemoval()
    {
        var scenario = await CreateInventory();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var player = Client.Session!.AttachedEntity!.Value;
            var context = ui.GetUIController<ContextMenuUIController>();
            context.Setup();
            var menu = ui.GetUIController<EntityMenuUIController>();
            Set(menu, "_updating", true);
            Set(menu, "_xformQuery", CEntMan.GetEntityQuery<TransformComponent>());
            Set(menu, "_spriteQuery", CEntMan.GetEntityQuery<SpriteComponent>());
            var targets = scenario.Targets.Select(CEntMan.GetEntity).ToList();
            menu.OpenRootMenu(targets);
            var examiner = CEntMan.GetComponent<ExaminerComponent>(player);
            examiner.SkipChecks = true;
            menu.FrameUpdate(new FrameEventArgs(0));
            Assert.That(menu.Elements.Keys, Is.EquivalentTo(targets));

            Client.System<SpriteSystem>().SetVisible(targets[0], false);
            menu.FrameUpdate(new FrameEventArgs(0));
            Assert.That(menu.Elements.Keys, Is.EquivalentTo(targets.Skip(1)));
            examiner.SkipChecks = false;
            examiner.CheckInRangeUnOccluded = true;
            Client.System<SharedTransformSystem>().SetWorldPosition(targets[1], new Vector2(500, 500));
            menu.FrameUpdate(new FrameEventArgs(0));
            Assert.That(menu.Elements.ContainsKey(targets[1]), Is.False, "A previously visible entity must still pass live range/LOS checks.");
            Client.System<SpriteSystem>().SetVisible(targets[2], false);
            menu.FrameUpdate(new FrameEventArgs(0));
            Assert.That(menu.Elements, Is.Empty);
            Assert.That(context.RootMenu.Visible, Is.False);
            Set(menu, "_updating", false);
        });
    }

    [Test]
    public async Task IconLabelRelabelingAndInPlaceParameterChangesInvalidatePresentation()
    {
        await Client.WaitAssertion(() =>
        {
            var localization = Client.ResolveDependency<ILocalizationManager>();
            var cache = new CMUIconLabelPresentation();
            var label = new IconLabelComponent
            {
                LabelTextLocId = "rmc-custom-container-label-text",
                LabelTextParams = [("customLabel", "ABCD")],
                LabelMaxSize = 3,
                TextColor = "Red",
            };
            var now = CGameTiming.RealTime;
            Assert.That(cache.TryGet(label, localization, now, out var text, out var color), Is.True);
            Assert.That(text, Is.EqualTo("ABC"));
            Assert.That(color, Is.EqualTo(Color.Red));
            label.LabelTextParams[0] = ("customLabel", "XYZQ");
            Assert.That(cache.TryGet(label, localization, now, out text, out color), Is.True);
            Assert.That(text, Is.EqualTo("XYZ"), "An in-place parameter edit must invalidate unchanged label settings.");
            Assert.That(color, Is.EqualTo(Color.Red));
            label.LabelMaxSize = 2;
            Assert.That(cache.TryGet(label, localization, now, out text, out color), Is.True);
            Assert.That(text, Is.EqualTo("XY"));
            label.TextColor = "Blue";
            Assert.That(cache.TryGet(label, localization, now, out _, out color), Is.True);
            Assert.That(color, Is.EqualTo(Color.Blue));
            label.LabelTextLocId = null;
            Assert.That(cache.TryGet(label, localization, now, out _, out _), Is.False);
            label.LabelTextParams.Clear();
            label.LabelTextLocId = "rmc-emergency-medicine-container-label-text";
            Assert.That(cache.TryGet(label, localization, now, out text, out _), Is.True);
            Assert.That(text, Is.EqualTo(Loc.GetString(label.LabelTextLocId.Value)));
        });
    }

    private async Task<(NetEntity Bag, NetEntity Held, List<NetEntity> Targets)> CreateInventory()
    {
        var map = await Pair.CreateTestMap();
        NetEntity bagNet = default;
        NetEntity heldNet = default;
        var targets = new List<NetEntity>();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bag = SEntMan.SpawnEntity("CMUUiRegressionStorage", map.GridCoords);
            var held = SEntMan.SpawnEntity("CMUUiRegressionItem", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(user, bag, "back", force: true), Is.True);
            Assert.That(Server.System<HandsSystem>().TryPickupAnyHand(user, held, checkActionBlocker: false), Is.True);
            var item = SEntMan.SpawnEntity("CMUUiRegressionItem", map.GridCoords);
            Assert.That(Server.System<SharedStorageSystem>().InsertAt(bag, item, new ItemStorageLocation(Angle.Zero, Vector2i.Zero), out _, playSound: false), Is.True);
            for (var i = 0; i < 3; i++)
                targets.Add(SEntMan.GetNetEntity(SEntMan.SpawnEntity(i == 2 ? "CMUUiRegressionStorage" : "CMUUiRegressionItem", map.GridCoords)));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            bagNet = SEntMan.GetNetEntity(bag);
            heldNet = SEntMan.GetNetEntity(held);
        });
        await Pair.RunUntilSynced();
        return (bagNet, heldNet, targets);
    }

    private static string CooldownText(int duration, int remaining) => FormattedMessage.FromMarkupOrThrow(
        Loc.GetString("ui-actionslot-duration", ("duration", duration), ("timeLeft", remaining))).ToMarkup();

    private static T Get<T>(object target, string field) =>
        (T) target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Set(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static Action Frame(Control control)
    {
        var update = control.GetType().GetMethod("FrameUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<FrameEventArgs>>(control);
        return () => update(new FrameEventArgs(1f / 60));
    }
}
