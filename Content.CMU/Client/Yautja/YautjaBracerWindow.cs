using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Yautja;

// the bracer drawn as the same predator hud as the clan hall. functions on the left in four tabs,
// each row says what it does and what state it's in right now; the tracker scope sits on the right.
// every press goes to the server, the next state it sends is what the rows show
public sealed partial class YautjaBracerWindow : BaseWindow
{
    private const float ResizeGrip = 16;
    private const int TrackerRows = 12;
    private static readonly string[] DirectionNames = { "N", "NE", "SE", "S", "SW", "NW" };

    [Dependency] private IGameTiming _timing = default!;

    private readonly Font _title;
    private readonly Font _label;
    private readonly Font _small;
    private readonly Font _body;
    private readonly Font _bodyBold;

    private readonly Dictionary<BracerTab, HudButton> _tabButtons = new();
    private readonly Dictionary<BracerTab, Control> _pages = new();
    private readonly Dictionary<YautjaBracerPanelCommand, FunctionRow> _rows = new();

    private readonly HudBar _powerBar;
    private readonly Label _powerValue;
    private readonly HudTag _lockTag;
    private readonly HudTag _idTag;
    private readonly HudTag _coreTag;

    private readonly Label _thrallName;
    private readonly HudTag _thrallTag;

    private readonly YautjaBracerScope _scope;
    private readonly Label _trackerSummary;
    private readonly Label _nearest;
    private readonly Label[] _bucketCells = new Label[6];
    private readonly TrackerRow[] _trackerRows = new TrackerRow[TrackerRows];
    private readonly Label _trackerEmpty;

    private YautjaBracerPanelState? _state;
    private TimeSpan _nextTick;

    public event Action<YautjaBracerPanelCommand>? OnCommand;

    public YautjaBracerWindow()
    {
        IoCManager.InjectDependencies(this);

        _title = Hud.Font(Hud.Osd, 14);
        _label = Hud.Font(Hud.Osd, 10);
        _small = Hud.Font(Hud.Osd, 8);
        _body = Hud.Font(Hud.Sans, 12);
        _bodyBold = Hud.Font(Hud.SansBold, 12);

        Resizable = true;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(820, 560);
        SetSize = new Vector2(940, 620);

        var background = new HudBackground { HorizontalExpand = true, VerticalExpand = true };
        AddChild(background);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10, Margin = new Thickness(18, 14) };
        background.AddChild(root);

        // header: name, power, the three states that matter most, close
        var header = HBox(10);
        var title = Hud.Write(Loc.GetString("cmu-yautja-bracer-menu-title").ToUpperInvariant(), _title, Hud.Bright);
        title.VerticalAlignment = VAlignment.Center;
        title.Margin = new Thickness(0, 0, 8, 0);
        header.AddChild(title);

        header.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-menu-power")));
        _powerBar = new HudBar(0) { MinSize = new Vector2(160, 10) };
        header.AddChild(_powerBar);
        _powerValue = Hud.Write(string.Empty, _label, Hud.Text);
        _powerValue.VerticalAlignment = VAlignment.Center;
        _powerValue.MinWidth = 80;
        header.AddChild(_powerValue);

        header.AddChild(Gap());
        _lockTag = new HudTag(string.Empty, _small);
        _idTag = new HudTag(string.Empty, _small);
        _coreTag = new HudTag(string.Empty, _small);
        header.AddChild(_lockTag);
        header.AddChild(_idTag);
        header.AddChild(_coreTag);

        var close = new HudButton("X", _label);
        close.Activated += Close;
        close.Margin = new Thickness(8, 0, 0, 0);
        header.AddChild(close);
        root.AddChild(header);

        var body = HBox(14);
        body.VerticalExpand = true;
        root.AddChild(body);

        // left: the function tabs
        var left = VBox(8);
        left.HorizontalExpand = true;
        left.VerticalExpand = true;
        body.AddChild(left);

        var tabs = HBox(6);
        left.AddChild(tabs);
        var area = new Control { HorizontalExpand = true, VerticalExpand = true };
        left.AddChild(area);

        _thrallName = Hud.Write(string.Empty, _bodyBold, Hud.Text, clip: true);
        _thrallTag = new HudTag(string.Empty, _small);

        AddTab(tabs, area, BracerTab.Hunt, "cmu-yautja-bracer-ui-tab-hunt", BuildHuntPage());
        AddTab(tabs, area, BracerTab.Thrall, "cmu-yautja-bracer-ui-tab-thrall", BuildThrallPage());
        AddTab(tabs, area, BracerTab.Fabricator, "cmu-yautja-bracer-ui-tab-fabricator", BuildFabricatorPage());
        AddTab(tabs, area, BracerTab.Bracer, "cmu-yautja-bracer-ui-tab-bracer", BuildBracerPage());

        // right: the tracker
        var tracker = new HudFrame(Loc.GetString("cmu-yautja-bracer-menu-tracker"), _small)
        {
            HorizontalExpand = false,
            VerticalExpand = true,
            SetWidth = 340,
        };
        body.AddChild(tracker);

        _trackerSummary = Hud.Write(string.Empty, _small, Hud.Muted, clip: true);
        _trackerSummary.HorizontalExpand = true;
        _trackerSummary.VerticalAlignment = VAlignment.Center;
        tracker.Header.AddChild(_trackerSummary);

        _scope = new YautjaBracerScope(_small);
        tracker.Body.AddChild(_scope);

        _nearest = Hud.Write(string.Empty, _small, Hud.Label, clip: true);
        tracker.Body.AddChild(_nearest);
        tracker.Body.AddChild(BuildBucketGrid());

        var listScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        var list = VBox(0);
        list.HorizontalExpand = true;
        listScroll.AddChild(list);
        tracker.Body.AddChild(listScroll);

        _trackerEmpty = Hud.Write(Loc.GetString("cmu-yautja-bracer-menu-tracker-empty"), _body, Hud.Muted);
        list.AddChild(_trackerEmpty);
        for (var i = 0; i < TrackerRows; i++)
        {
            var row = new TrackerRow(this, i);
            _trackerRows[i] = row;
            list.AddChild(row);
        }

        var trackerButtons = HBox(6);
        trackerButtons.AddChild(CommandButton("cmu-yautja-bracer-menu-add-tracked-item", YautjaBracerPanelCommand.AddTrackedItem));
        trackerButtons.AddChild(CommandButton("cmu-yautja-bracer-menu-remove-tracked-item", YautjaBracerPanelCommand.RemoveTrackedItem));
        var handHint = Hud.Write(Loc.GetString("cmu-yautja-bracer-ui-tracker-hand"), _small, Hud.Muted, clip: true);
        handHint.HorizontalExpand = true;
        handHint.VerticalAlignment = VAlignment.Center;
        trackerButtons.AddChild(handHint);
        tracker.Body.AddChild(trackerButtons);

        ShowTab(BracerTab.Hunt);
    }

    private enum BracerTab : byte
    {
        Hunt,
        Thrall,
        Fabricator,
        Bracer,
    }

    // pages

    private Control BuildHuntPage()
    {
        var page = VBox(6);
        page.AddChild(Row(YautjaBracerPanelCommand.OpenMarks, "cmu-yautja-bracer-menu-marks", "cmu-yautja-bracer-ui-marks-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.OpenTranslator, "cmu-yautja-bracer-menu-translator", "cmu-yautja-bracer-ui-translator-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.OpenClanHall, "cmu-yautja-bracer-menu-clan-hall", "cmu-yautja-bracer-menu-clan-hall-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.RemoteExecuteYoungblood, "cmu-yautja-bracer-menu-youngblood-execute", "cmu-yautja-bracer-ui-youngblood-detail", danger: true));
        return page;
    }

    private Control BuildThrallPage()
    {
        var page = VBox(6);

        var who = HBox(8);
        who.Margin = new Thickness(2, 0, 0, 4);
        who.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-menu-thrall")));
        _thrallName.HorizontalExpand = true;
        _thrallName.VerticalAlignment = VAlignment.Center;
        who.AddChild(_thrallName);
        who.AddChild(_thrallTag);
        page.AddChild(who);

        page.AddChild(Row(YautjaBracerPanelCommand.LinkThrallBracer, "cmu-yautja-bracer-menu-link-thrall", "cmu-yautja-bracer-ui-link-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.OpenThrallTransmission, "cmu-yautja-bracer-menu-thrall-message", "cmu-yautja-bracer-ui-message-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleThrallBracerLock, "cmu-yautja-bracer-menu-toggle-thrall-lock", "cmu-yautja-bracer-ui-thrall-lock-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.StunThrall, "cmu-yautja-bracer-menu-stun-thrall", "cmu-yautja-bracer-ui-stun-detail", danger: true));
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleThrallSelfDestruct, "cmu-yautja-bracer-menu-thrall-destruct", "cmu-yautja-bracer-ui-thrall-destruct-detail", danger: true));
        return page;
    }

    private Control BuildFabricatorPage()
    {
        var page = VBox(6);
        var hint = Hud.Write(Loc.GetString("cmu-yautja-bracer-ui-fabricator-hint"), _small, Hud.Muted);
        hint.Margin = new Thickness(2, 0, 0, 4);
        page.AddChild(hint);
        page.AddChild(Row(YautjaBracerPanelCommand.CreateStabilisingCrystal, "cmu-yautja-bracer-menu-crystal", "cmu-yautja-bracer-menu-crystal-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.CreateHumanStabilisingCrystal, "cmu-yautja-bracer-menu-human-crystal", "cmu-yautja-bracer-menu-human-crystal-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.CreateHealingCapsule, "cmu-yautja-bracer-menu-capsule", "cmu-yautja-bracer-menu-capsule-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.CreateHuntingTrap, "cmu-yautja-bracer-menu-hunting-trap", "cmu-yautja-bracer-menu-hunting-trap-detail"));
        return page;
    }

    private Control BuildBracerPage()
    {
        var page = VBox(6);
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleBracerLock, "cmu-yautja-bracer-menu-toggle-lock", "cmu-yautja-bracer-ui-lock-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleBracerIdChip, "cmu-yautja-bracer-menu-toggle-id", "cmu-yautja-bracer-ui-id-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleBracerName, "cmu-yautja-bracer-menu-toggle-name", "cmu-yautja-bracer-menu-toggle-name-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleBracerNotificationSound, "cmu-yautja-bracer-menu-toggle-sound", "cmu-yautja-bracer-menu-toggle-sound-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.RemoveBracerAttachments, "cmu-yautja-bracer-menu-remove-attachments", "cmu-yautja-bracer-menu-remove-attachments-detail"));
        page.AddChild(Row(YautjaBracerPanelCommand.ChangeExplosionType, "cmu-yautja-bracer-menu-explosion-type", "cmu-yautja-bracer-menu-explosion-type-detail", danger: true));
        page.AddChild(Row(YautjaBracerPanelCommand.ToggleSelfDestruct, "cmu-yautja-bracer-menu-toggle-destruct", "cmu-yautja-bracer-ui-destruct-detail", danger: true));
        return page;
    }

    private Control BuildBucketGrid()
    {
        // fallen hunters and loose tech, split by where they are. counts here include other maps,
        // which the scope can't plot
        var grid = new GridContainer { Columns = 4, HorizontalExpand = true };
        grid.AddChild(new Control());
        grid.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-ui-bucket-grounds")));
        grid.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-ui-bucket-orbit")));
        grid.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-ui-bucket-low-orbit")));

        grid.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-ui-bucket-fallen")));
        for (var i = 0; i < 3; i++)
            grid.AddChild(_bucketCells[i] = Hud.Write("0", _label, Hud.Dim));

        grid.AddChild(Caption(Loc.GetString("cmu-yautja-bracer-ui-bucket-tech")));
        for (var i = 3; i < 6; i++)
            grid.AddChild(_bucketCells[i] = Hud.Write("0", _label, Hud.Dim));

        foreach (var child in grid.Children)
        {
            child.HorizontalExpand = true;
            if (child is Label label)
                label.Align = Label.AlignMode.Center;
        }

        return grid;
    }

    private FunctionRow Row(YautjaBracerPanelCommand command, string titleLoc, string detailLoc, bool danger = false)
    {
        var row = new FunctionRow(Loc.GetString(titleLoc), Loc.GetString(detailLoc), _bodyBold, _small, danger);
        row.OnPressed += _ => OnCommand?.Invoke(command);
        _rows[command] = row;
        return row;
    }

    private HudButton CommandButton(string loc, YautjaBracerPanelCommand command)
    {
        var button = new HudButton(Loc.GetString(loc), _small);
        button.Activated += () => OnCommand?.Invoke(command);
        return button;
    }

    private void AddTab(BoxContainer tabs, Control area, BracerTab tab, string loc, Control content)
    {
        var button = new HudButton(Loc.GetString(loc).ToUpperInvariant(), _label);
        button.Activated += () => ShowTab(tab);
        tabs.AddChild(button);
        _tabButtons[tab] = button;

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true, Visible = false };
        content.HorizontalExpand = true;
        scroll.AddChild(content);
        area.AddChild(scroll);
        _pages[tab] = scroll;
    }

    private void ShowTab(BracerTab tab)
    {
        foreach (var (key, control) in _pages)
            control.Visible = key == tab;
        foreach (var (key, button) in _tabButtons)
            button.Selected = key == tab;
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (relativeMousePos.X >= Size.X - ResizeGrip && relativeMousePos.Y >= Size.Y - ResizeGrip)
            return DragMode.Bottom | DragMode.Right;

        return DragMode.Move;
    }

    // state

    public void UpdateState(YautjaBracerPanelState state)
    {
        _state = state;

        _powerBar.Fill = state.MaxCharge > 0 ? state.Charge / (float) state.MaxCharge : 0;
        _powerValue.Text = Loc.GetString("cmu-yautja-bracer-menu-power-value", ("charge", state.Charge), ("max", state.MaxCharge));

        Paint(_lockTag, Loc.GetString(state.Locked ? "cmu-yautja-bracer-menu-state-locked" : "cmu-yautja-bracer-menu-state-open"),
            state.Locked ? Hud.Bright : Hud.Muted, !state.Locked);
        Paint(_idTag, Loc.GetString("cmu-yautja-bracer-ui-id-tag", ("state", Loc.GetString(state.IdChipDeployed ? "cmu-yautja-bracer-menu-state-deployed" : "cmu-yautja-bracer-menu-state-stowed"))),
            state.IdChipDeployed ? Hud.Bright : Hud.Muted, !state.IdChipDeployed);

        UpdateThrall(state);
        UpdateBracer(state);
        UpdateTimed();
        UpdateTracker(state);
    }

    private void UpdateThrall(YautjaBracerPanelState state)
    {
        var hasThrall = !string.IsNullOrWhiteSpace(state.ThrallName);
        _thrallName.Text = hasThrall ? state.ThrallName! : Loc.GetString("cmu-yautja-bracer-ui-no-thrall");
        _thrallName.FontColorOverride = hasThrall ? Hud.Text : Hud.Muted;
        _thrallTag.Visible = hasThrall;
        Paint(_thrallTag, Loc.GetString(state.ThrallLinked ? "cmu-yautja-bracer-menu-state-linked" : "cmu-yautja-bracer-menu-state-unlinked"),
            state.ThrallLinked ? Hud.Bright : Hud.Muted, !state.ThrallLinked);

        _rows[YautjaBracerPanelCommand.LinkThrallBracer].SetTag(
            state.ThrallLinked ? Loc.GetString("cmu-yautja-bracer-menu-state-linked") : null);

        // everything past linking talks to the thrall's bracer, so it waits for the link
        var reason = !hasThrall
            ? Loc.GetString("cmu-yautja-bracer-ui-no-thrall")
            : !state.ThrallLinked
                ? Loc.GetString("cmu-yautja-bracer-menu-state-unlinked")
                : null;

        _rows[YautjaBracerPanelCommand.OpenThrallTransmission].SetTag(null, unavailable: reason);
        _rows[YautjaBracerPanelCommand.StunThrall].SetTag(null, unavailable: reason);
        _rows[YautjaBracerPanelCommand.ToggleThrallBracerLock].SetTag(
            Loc.GetString(state.ThrallBracerLocked ? "cmu-yautja-bracer-menu-state-locked" : "cmu-yautja-bracer-menu-state-open"),
            unavailable: reason);

        var thrallDestruct = _rows[YautjaBracerPanelCommand.ToggleThrallSelfDestruct];
        thrallDestruct.SetTag(state.ThrallSelfDestructArmed ? Loc.GetString("cmu-yautja-bracer-menu-state-armed") : null,
            hot: state.ThrallSelfDestructArmed, unavailable: reason);
    }

    private void UpdateBracer(YautjaBracerPanelState state)
    {
        var lockRow = _rows[YautjaBracerPanelCommand.ToggleBracerLock];
        lockRow.SetTitle(Loc.GetString(state.Locked ? "cmu-yautja-bracer-menu-unlock" : "cmu-yautja-bracer-menu-toggle-lock"));
        lockRow.SetTag(Loc.GetString(state.Locked ? "cmu-yautja-bracer-menu-state-locked" : "cmu-yautja-bracer-menu-state-open"));

        _rows[YautjaBracerPanelCommand.ToggleBracerIdChip].SetTag(
            Loc.GetString(state.IdChipDeployed ? "cmu-yautja-bracer-menu-state-deployed" : "cmu-yautja-bracer-menu-state-stowed"));
        _rows[YautjaBracerPanelCommand.ToggleBracerName].SetTag(
            Loc.GetString(state.NameVisible ? "cmu-yautja-bracer-ui-shown" : "cmu-yautja-bracer-ui-hidden"));
        _rows[YautjaBracerPanelCommand.ToggleBracerNotificationSound].SetTag(
            Loc.GetString(state.NotificationSound ? "cmu-yautja-bracer-ui-on" : "cmu-yautja-bracer-ui-off"));
        _rows[YautjaBracerPanelCommand.RemoveBracerAttachments].SetTag(null);
        _rows[YautjaBracerPanelCommand.ChangeExplosionType].SetTag(
            Loc.GetString(state.ExplosionType == YautjaSelfDestructExplosionType.Big ? "cmu-yautja-bracer-ui-blast-big" : "cmu-yautja-bracer-ui-blast-small"));

        _rows[YautjaBracerPanelCommand.ToggleSelfDestruct].SetTitle(Loc.GetString(state.SelfDestructArmed
            ? "cmu-yautja-bracer-menu-cancel-destruct"
            : "cmu-yautja-bracer-menu-toggle-destruct"));
    }

    // cooldowns and the self destruct count down between server states, so they tick on the client
    private void UpdateTimed()
    {
        if (_state is not { } state)
            return;

        var now = _timing.CurTime;

        var armed = state.SelfDestructArmed;
        var left = armed ? Countdown(state.SelfDestructAt - now) : null;
        var core = armed
            ? Loc.GetString("cmu-yautja-bracer-ui-core-armed", ("time", left ?? "0:00"))
            : Loc.GetString("cmu-yautja-bracer-ui-core-standby");
        Paint(_coreTag, core, armed ? Hud.Hot : Hud.Muted, !armed);
        _rows[YautjaBracerPanelCommand.ToggleSelfDestruct].SetTag(
            armed ? core : Loc.GetString("cmu-yautja-bracer-menu-state-standby"), hot: armed);

        foreach (var recipe in state.Fabricator)
        {
            if (!_rows.TryGetValue(recipe.Command, out var row))
                continue;

            var cost = Loc.GetString("cmu-yautja-bracer-ui-cost", ("cost", recipe.Cost));
            string? unavailable = null;
            if (!recipe.Available)
                unavailable = Loc.GetString("cmu-yautja-bracer-ui-offline");
            else if (recipe.ReadyAt > now)
                unavailable = Loc.GetString("cmu-yautja-bracer-ui-cycling", ("time", Countdown(recipe.ReadyAt - now)));
            else if (state.Charge < recipe.Cost)
                unavailable = Loc.GetString("cmu-yautja-bracer-ui-low-power", ("cost", recipe.Cost));

            row.SetTag(cost, unavailable: unavailable);
        }
    }

    private void UpdateTracker(YautjaBracerPanelState state)
    {
        var entries = state.TrackedGear;
        _scope.Set(entries);

        var total = 0;
        foreach (var entry in entries)
            total += Math.Max(1, entry.Count);

        _trackerSummary.Text = entries.Count == 0
            ? string.Empty
            : Loc.GetString("cmu-yautja-bracer-ui-tracker-summary", ("count", total)).ToUpperInvariant();

        var readout = state.TrackerReadout;
        SetBucket(0, readout.DeadHuntingGrounds);
        SetBucket(1, readout.DeadOrbit);
        SetBucket(2, readout.DeadLowOrbit);
        SetBucket(3, readout.GearHuntingGrounds);
        SetBucket(4, readout.GearOrbit);
        SetBucket(5, readout.GearLowOrbit);

        if (readout.ClosestPresent)
        {
            _nearest.Text = Loc.GetString("cmu-yautja-bracer-ui-nearest",
                ("distance", readout.ClosestDistance),
                ("direction", DirectionName(readout.ClosestDirection)),
                ("area", string.IsNullOrWhiteSpace(readout.ClosestArea)
                    ? Loc.GetString("cmu-yautja-tracker-unknown-area")
                    : readout.ClosestArea)).ToUpperInvariant();
        }
        else
        {
            _nearest.Text = Loc.GetString("cmu-yautja-bracer-ui-nearest-none").ToUpperInvariant();
        }

        _trackerEmpty.Visible = entries.Count == 0;
        for (var i = 0; i < _trackerRows.Length; i++)
            _trackerRows[i].Set(i < entries.Count ? entries[i] : null);

        if (_scope.Highlight >= entries.Count)
            _scope.Highlight = -1;
    }

    private void SetBucket(int index, int count)
    {
        var cell = _bucketCells[index];
        cell.Text = count.ToString();
        cell.FontColorOverride = count > 0 ? Hud.Hot : Hud.Dim;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var now = _timing.RealTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TimeSpan.FromSeconds(0.25);
        UpdateTimed();
    }

    // helpers

    private static void Paint(HudTag tag, string text, Color color, bool outline)
    {
        tag.Text = text;
        tag.Outline = false;
        tag.Fill = color;
        tag.Outline = outline;
    }

    private static string Countdown(TimeSpan left)
    {
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;

        var seconds = (int) Math.Ceiling(left.TotalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static string DirectionName(byte direction)
    {
        return direction < DirectionNames.Length ? DirectionNames[direction] : "?";
    }

    private static BoxContainer HBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    private static BoxContainer VBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    private static Control Gap()
    {
        return new Control { HorizontalExpand = true };
    }

    private Label Caption(string text)
    {
        var label = Hud.Write(text.ToUpperInvariant(), _small, Hud.Muted);
        label.VerticalAlignment = VAlignment.Center;
        return label;
    }

    // one bracer function: what it is, one line on what it does, and its state on the right.
    // unavailable rows go dim and the tag says why instead
    private sealed class FunctionRow : ContainerButton
    {
        private readonly bool _danger;
        private readonly Label _title;
        private readonly Label _detail;
        private readonly HudTag _tag;

        public FunctionRow(string title, string detail, Font titleFont, Font small, bool danger)
        {
            _danger = danger;
            HorizontalExpand = true;
            MinHeight = 44;

            var line = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 10,
                Margin = new Thickness(14, 5, 12, 5),
            };

            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            _title = Hud.Write(title.ToUpperInvariant(), titleFont, Hud.Text, clip: true);
            _detail = Hud.Write(detail, small, Hud.Muted, clip: true);
            text.AddChild(_title);
            text.AddChild(_detail);
            line.AddChild(text);

            _tag = new HudTag(string.Empty, small) { Visible = false };
            line.AddChild(_tag);
            AddChild(line);
        }

        public void SetTitle(string title)
        {
            _title.Text = title.ToUpperInvariant();
        }

        public void SetTag(string? text, bool hot = false, string? unavailable = null)
        {
            Disabled = unavailable != null;
            var shown = unavailable ?? text;
            _tag.Visible = shown != null;
            if (shown != null)
            {
                var color = Disabled ? Hud.Muted : hot ? Hud.Hot : _danger ? Hud.Label : Hud.Bright;
                Paint(_tag, shown, color, !hot);
            }

            _title.FontColorOverride = Disabled ? Hud.Muted : Hud.Text;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var s = UIScale;
            var w = PixelSize.X - 1;
            var h = PixelSize.Y - 1;
            var cut = 8 * s;
            var points = new[] { new Vector2(0, 0), new Vector2(w - cut, 0), new Vector2(w, cut), new Vector2(w, h), new Vector2(0, h) };

            var hover = !Disabled && DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;
            var accent = _danger ? Hud.Hot : Hud.Bright;
            if (DrawMode == DrawModeEnum.Pressed && !Disabled)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, accent.WithAlpha(0.22f));
            else if (hover)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, accent.WithAlpha(0.09f));

            var edge = Disabled ? Hud.Dim.WithAlpha(0.6f) : hover ? accent : Hud.Dim;
            for (var i = 0; i < points.Length; i++)
                handle.DrawLine(points[i], points[(i + 1) % points.Length], edge);

            // a lit bar down the left edge: red for normal, orange for anything that hurts someone
            var bar = UIBox2.FromDimensions(0, 0, 3 * s, PixelSize.Y);
            handle.DrawRect(bar, Disabled ? Hud.Dim : hover ? accent : accent.WithAlpha(0.7f));

            base.Draw(handle);
        }
    }

    // a tracker signal in the list. hovering lights its blip on the scope
    private sealed class TrackerRow : HudRow
    {
        private readonly YautjaBracerWindow _window;
        private readonly int _index;
        private readonly Label _direction;
        private readonly Label _name;
        private readonly Label _range;

        public TrackerRow(YautjaBracerWindow window, int index)
        {
            _window = window;
            _index = index;

            var line = HBox(8);
            line.Margin = new Thickness(8, 3, 4, 3);
            _direction = Hud.Write(string.Empty, window._label, Hud.Bright);
            _direction.MinWidth = 26;
            _direction.VerticalAlignment = VAlignment.Center;
            line.AddChild(_direction);

            _name = Hud.Write(string.Empty, window._body, Hud.Text, clip: true);
            _name.HorizontalExpand = true;
            _name.VerticalAlignment = VAlignment.Center;
            line.AddChild(_name);

            _range = Hud.Write(string.Empty, window._label, Hud.Label);
            _range.Align = Label.AlignMode.Right;
            _range.MinWidth = 52;
            _range.VerticalAlignment = VAlignment.Center;
            line.AddChild(_range);
            AddChild(line);

            Visible = false;
        }

        public void Set(YautjaGearTrackerEntry? entry)
        {
            Visible = entry != null;
            if (entry == null)
                return;

            _direction.Text = DirectionName(entry.Direction);
            _name.Text = entry.Count > 1
                ? Loc.GetString("cmu-yautja-bracer-ui-tracker-group", ("count", entry.Count), ("names", entry.Name))
                : entry.Name;
            _range.Text = $"{entry.Distance}m";
            // long names clip in the row, the tooltip carries the full one
            ToolTip = $"{_name.Text}\n{Loc.GetString("cmu-yautja-bracer-menu-tracker-entry-detail", ("distance", entry.Distance), ("bearing", entry.Bearing))}";
        }

        protected override void MouseEntered()
        {
            base.MouseEntered();
            _window._scope.Highlight = _index;
        }

        protected override void MouseExited()
        {
            base.MouseExited();
            if (_window._scope.Highlight == _index)
                _window._scope.Highlight = -1;
        }
    }
}
