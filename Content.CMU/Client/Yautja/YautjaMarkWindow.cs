using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Yautja;

// the hunt marks, drawn as the same predator hud as the bracer and the clan hall. left is who you have seen,
// right is the dossier on whoever you picked: what they carry, what you put on them, and what you can do about it.
// nothing here decides anything, the server answers every press with a fresh state
public sealed class YautjaMarkWindow : BaseWindow
{
    private const float ResizeGrip = 16;
    private const int RowTags = 3;

    private static readonly YautjaMarkKind[] Kinds =
    [
        YautjaMarkKind.Prey,
        YautjaMarkKind.Dishonored,
        YautjaMarkKind.Honored,
        YautjaMarkKind.GearCarrier,
        YautjaMarkKind.Thrall,
        YautjaMarkKind.Blooded,
        YautjaMarkKind.Student,
    ];

    private readonly Font _title;
    private readonly Font _label;
    private readonly Font _small;
    private readonly Font _big;
    private readonly Font _body;
    private readonly Font _bodyBold;

    private readonly HudButton _recentTab;
    private readonly HudButton _historyTab;
    private readonly HudButton _previous;
    private readonly HudButton _next;
    private readonly Label _page;
    private readonly Label _viewHint;
    private readonly Label _summary;
    private readonly BoxContainer _targets;

    private readonly Label _name;
    private readonly HudTag _typeTag;
    private readonly HudTag _reachTag;
    private readonly GridContainer _markTags;
    private readonly BoxContainer _ownedSection;
    private readonly BoxContainer _ownedButtons;
    private readonly Dictionary<YautjaMarkKind, HudButton> _kindButtons = new();
    private readonly RichTextLabel _kindDetail;
    private readonly LineEdit _reason;
    private readonly HudButton _mark;
    private readonly HudButton _change;
    private readonly HudButton _remove;
    private readonly RichTextLabel _status;

    private readonly List<YautjaMarkPanelEntry> _entries = new();
    private int? _selectedRecord;
    private YautjaMarkKind _desired = YautjaMarkKind.Prey;
    private YautjaMarkKind? _existing;
    private uint _revision;
    private bool _history;
    private int _pageIndex;
    private int _pages = 1;

    public event Action<int, uint, YautjaMarkKind, string?>? OnMark;
    public event Action<int, uint, YautjaMarkKind>? OnUnmark;
    public event Action<int, uint, YautjaMarkKind, YautjaMarkKind, string?>? OnChange;
    public event Action<bool, int>? OnRefresh;

    public YautjaMarkWindow()
    {
        _title = Hud.Font(Hud.Osd, 14);
        _label = Hud.Font(Hud.Osd, 10);
        _small = Hud.Font(Hud.Osd, 8);
        _big = Hud.Font(Hud.Osd, 18);
        _body = Hud.Font(Hud.Sans, 12);
        _bodyBold = Hud.Font(Hud.SansBold, 12);

        Resizable = true;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(780, 520);
        SetSize = new Vector2(860, 580);

        var background = new HudBackground { HorizontalExpand = true, VerticalExpand = true };
        AddChild(background);
        var root = VBox(10);
        root.Margin = new Thickness(18, 14);
        background.AddChild(root);

        // header: name, the two views, paging for the long one, close
        var header = HBox(6);
        var title = Hud.Write(Loc.GetString("cmu-yautja-mark-window-title").ToUpperInvariant(), _title, Hud.Bright);
        title.VerticalAlignment = VAlignment.Center;
        title.Margin = new Thickness(0, 0, 18, 0);
        header.AddChild(title);

        _recentTab = new HudButton(Loc.GetString("cmu-yautja-mark-view-recent").ToUpperInvariant(), _label);
        _recentTab.Activated += () => OnRefresh?.Invoke(false, 0);
        header.AddChild(_recentTab);
        _historyTab = new HudButton(Loc.GetString("cmu-yautja-mark-view-history").ToUpperInvariant(), _label);
        _historyTab.Activated += () => OnRefresh?.Invoke(true, 0);
        header.AddChild(_historyTab);

        header.AddChild(new Control { HorizontalExpand = true });
        _previous = new HudButton("<", _label);
        _previous.Activated += () => OnRefresh?.Invoke(true, Math.Max(0, _pageIndex - 1));
        header.AddChild(_previous);
        _page = Hud.Write(string.Empty, _label, Hud.Text);
        _page.VerticalAlignment = VAlignment.Center;
        _page.HorizontalAlignment = HAlignment.Center;
        _page.MinWidth = 64;
        header.AddChild(_page);
        _next = new HudButton(">", _label);
        _next.Activated += () => OnRefresh?.Invoke(true, Math.Min(_pages - 1, _pageIndex + 1));
        header.AddChild(_next);

        var close = new HudButton("X", _label);
        close.Margin = new Thickness(10, 0, 0, 0);
        close.Activated += Close;
        header.AddChild(close);
        root.AddChild(header);

        _viewHint = Hud.Write(string.Empty, _small, Hud.Muted, clip: true);
        _viewHint.HorizontalExpand = true;
        root.AddChild(_viewHint);

        // left: everyone the bracer has a record of
        var list = new HudFrame(Loc.GetString("cmu-yautja-mark-ui-targets"), _small) { VerticalExpand = true };
        _summary = Hud.Write(string.Empty, _small, Hud.Muted, clip: true);
        _summary.HorizontalExpand = true;
        _summary.VerticalAlignment = VAlignment.Center;
        list.Header.AddChild(_summary);
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        _targets = VBox(1);
        _targets.HorizontalExpand = true;
        scroll.AddChild(_targets);
        list.Body.VerticalExpand = true;
        list.Body.AddChild(scroll);

        // right: the dossier on the one you picked
        var dossier = new HudFrame(Loc.GetString("cmu-yautja-mark-ui-dossier"), _small) { VerticalExpand = true };
        var body = dossier.Body;
        body.SeparationOverride = 6;

        _name = Hud.Write(string.Empty, _big, Hud.Text, clip: true);
        _name.HorizontalExpand = true;
        body.AddChild(_name);

        var tags = HBox(6);
        _typeTag = new HudTag(string.Empty, _small) { Outline = true, Fill = Hud.Label };
        _reachTag = new HudTag(string.Empty, _small);
        tags.AddChild(_typeTag);
        tags.AddChild(_reachTag);
        body.AddChild(tags);

        body.AddChild(Caption(Loc.GetString("cmu-yautja-mark-ui-marks"), top: 6));
        _markTags = new GridContainer { Columns = 3, HSeparationOverride = 6, VSeparationOverride = 4 };
        body.AddChild(_markTags);

        _ownedSection = VBox(4);
        _ownedSection.AddChild(Caption(Loc.GetString("cmu-yautja-mark-ui-yours"), top: 4));
        _ownedButtons = HBox(6);
        _ownedSection.AddChild(_ownedButtons);
        body.AddChild(_ownedSection);

        body.AddChild(Caption(Loc.GetString("cmu-yautja-mark-ui-designate"), top: 8));
        var grid = new GridContainer { Columns = 2, HSeparationOverride = 6, VSeparationOverride = 6, HorizontalExpand = true };
        foreach (var kind in Kinds)
        {
            var button = new HudButton(Loc.GetString(YautjaMarkSystem.GetMarkName(kind)).ToUpperInvariant(), _label)
            {
                HorizontalExpand = true,
            };
            var captured = kind;
            button.Activated += () =>
            {
                _desired = captured;
                RefreshDossier();
            };
            _kindButtons[kind] = button;
            grid.AddChild(button);
        }
        body.AddChild(grid);

        _kindDetail = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(2, 2, 0, 0) };
        body.AddChild(_kindDetail);

        _reason = Hud.LineEdit();
        _reason.HorizontalExpand = true;
        _reason.OnTextChanged += _ => RefreshDossier();
        body.AddChild(_reason);

        var actions = HBox(6);
        actions.Margin = new Thickness(0, 4, 0, 0);
        _mark = new HudButton(Loc.GetString("cmu-yautja-mark-ui-mark").ToUpperInvariant(), _label) { HorizontalExpand = true };
        _mark.Activated += SendMark;
        _change = new HudButton(Loc.GetString("cmu-yautja-mark-ui-change").ToUpperInvariant(), _label) { HorizontalExpand = true };
        _change.Activated += SendChange;
        // removing is the one you can't take back, so it wants a second press
        _remove = new HudButton(Loc.GetString("cmu-yautja-mark-ui-remove").ToUpperInvariant(), _label, confirm: true) { HorizontalExpand = true };
        _remove.Activated += SendRemove;
        actions.AddChild(_mark);
        actions.AddChild(_change);
        actions.AddChild(_remove);
        body.AddChild(actions);

        _status = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(2, 2, 0, 0) };
        body.AddChild(_status);

        var columns = new HudColumns(list, dossier, 340) { VerticalExpand = true };
        root.AddChild(columns);

        RebuildTargets();
        RefreshDossier();
    }

    public void UpdateState(YautjaMarkPanelState state)
    {
        _entries.Clear();
        _entries.AddRange(state.Entries);
        if (_selectedRecord is { } selected && _entries.FindIndex(e => e.RecordId == selected) < 0)
            _selectedRecord = null;

        _revision = state.Revision;
        _history = state.History;
        _pageIndex = state.Page;
        _pages = Math.Max(1, state.Pages);

        RebuildTargets();
        RefreshDossier();
    }

    public void Select(int recordId)
    {
        _selectedRecord = recordId;
        _existing = null;
        RebuildTargets();
        RefreshDossier();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (relativeMousePos.X >= Size.X - ResizeGrip && relativeMousePos.Y >= Size.Y - ResizeGrip)
            return DragMode.Bottom | DragMode.Right;

        return DragMode.Move;
    }

    private YautjaMarkPanelEntry? Selected =>
        _selectedRecord is { } id ? _entries.Find(e => e.RecordId == id) : null;

    private void RebuildTargets()
    {
        _recentTab.Selected = !_history;
        _historyTab.Selected = _history;
        _previous.Visible = _history;
        _next.Visible = _history;
        _page.Visible = _history;
        _previous.Disabled = _pageIndex <= 0;
        _next.Disabled = _pageIndex >= _pages - 1;
        _page.Text = Loc.GetString("cmu-yautja-mark-page", ("page", _pageIndex + 1), ("pages", _pages));
        _viewHint.Text = Loc.GetString(_history
            ? "cmu-yautja-mark-view-history-detail"
            : "cmu-yautja-mark-view-recent-detail");
        _summary.Text = Loc.GetString(_history ? "cmu-yautja-mark-history-summary" : "cmu-yautja-mark-target-summary",
            ("count", _entries.Count), ("page", _pageIndex + 1), ("pages", _pages));

        _targets.RemoveAllChildren();
        if (_entries.Count == 0)
        {
            var empty = Hud.Write(Loc.GetString(_history ? "cmu-yautja-mark-no-history" : "cmu-yautja-mark-no-targets"),
                _body, Hud.Muted);
            empty.Margin = new Thickness(6, 8);
            _targets.AddChild(empty);
            return;
        }

        foreach (var entry in _entries)
            _targets.AddChild(BuildRow(entry));
    }

    private Control BuildRow(YautjaMarkPanelEntry entry)
    {
        var selected = entry.RecordId == _selectedRecord;
        var row = new HudRow { Selected = selected, MinHeight = 34 };
        var line = HBox(8);
        line.Margin = new Thickness(10, 5, 8, 5);
        row.AddChild(line);

        var type = new HudTag(TypeText(entry), _small) { Outline = true, Fill = entry.Available ? Hud.Label : Hud.Dim };
        type.MinWidth = 64;
        line.AddChild(type);

        var name = Hud.Write(entry.Name, selected ? _bodyBold : _body,
            !entry.Available ? Hud.Muted : selected ? Hud.Text : Hud.Label, clip: true);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        line.AddChild(name);

        if (!entry.Available)
        {
            var gone = Hud.Write(Loc.GetString("cmu-yautja-mark-ui-out-of-reach").ToUpperInvariant(), _small, Hud.Dim);
            gone.VerticalAlignment = VAlignment.Center;
            line.AddChild(gone);
        }

        for (var i = 0; i < entry.Marks.Count && i < RowTags; i++)
            line.AddChild(MarkTag(entry.Marks[i], entry.OwnedMarks.Contains(entry.Marks[i])));

        if (entry.Marks.Count > RowTags)
        {
            var more = Hud.Write(Loc.GetString("cmu-yautja-mark-ui-more", ("count", entry.Marks.Count - RowTags)),
                _small, Hud.Muted);
            more.VerticalAlignment = VAlignment.Center;
            line.AddChild(more);
        }

        var id = entry.RecordId;
        row.OnPressed += _ =>
        {
            _selectedRecord = id;
            _existing = null;
            RebuildTargets();
            RefreshDossier();
        };
        return row;
    }

    private void RefreshDossier()
    {
        var entry = Selected;
        foreach (var (kind, button) in _kindButtons)
            button.Selected = kind == _desired;

        Write(_kindDetail, Loc.GetString($"cmu-yautja-mark-{GetMarkKey(_desired)}-detail"), Hud.Label);
        var requiresReason = RequiresReason(_desired);
        _reason.PlaceHolder = Loc.GetString(requiresReason
            ? "cmu-yautja-mark-reason-required"
            : "cmu-yautja-mark-reason-placeholder");

        _markTags.RemoveAllChildren();
        _ownedButtons.RemoveAllChildren();

        if (entry == null)
        {
            _name.Text = Loc.GetString("cmu-yautja-mark-selection-none");
            _name.FontColorOverride = Hud.Muted;
            _typeTag.Visible = false;
            _reachTag.Visible = false;
            _ownedSection.Visible = false;
            _markTags.AddChild(Hud.Write(Loc.GetString("cmu-yautja-mark-none"), _body, Hud.Dim));
            _mark.Disabled = true;
            _change.Disabled = true;
            _remove.Disabled = true;
            SetStatus(Loc.GetString("cmu-yautja-mark-ui-pick-target"), false);
            return;
        }

        _name.Text = entry.Name;
        _name.FontColorOverride = entry.Available ? Hud.Text : Hud.Muted;
        _typeTag.Visible = true;
        _typeTag.Text = TypeText(entry);
        _reachTag.Visible = true;
        _reachTag.Text = Loc.GetString(entry.Available ? "cmu-yautja-mark-ui-in-reach" : "cmu-yautja-mark-ui-out-of-reach");
        _reachTag.Fill = entry.Available ? Hud.Bright : Hud.Dim;

        if (entry.Marks.Count == 0)
            _markTags.AddChild(Hud.Write(Loc.GetString("cmu-yautja-mark-none"), _body, Hud.Dim));
        foreach (var kind in entry.Marks)
            _markTags.AddChild(MarkTag(kind, entry.OwnedMarks.Contains(kind)));

        // the marks you can take back: your own, plus gear carrier, which anyone may clear once the gear is home
        var removable = GetRemovableMarks(entry);
        if (_existing is { } existing && !removable.Contains(existing))
            _existing = null;
        _existing ??= removable.Count > 0 ? removable[0] : null;
        _ownedSection.Visible = removable.Count > 0;
        foreach (var kind in removable)
        {
            var button = new HudButton(Loc.GetString(YautjaMarkSystem.GetMarkName(kind)).ToUpperInvariant(), _small)
            {
                Selected = kind == _existing,
            };
            var captured = kind;
            button.Activated += () =>
            {
                _existing = captured;
                RefreshDossier();
            };
            _ownedButtons.AddChild(button);
        }

        var desiredExists = entry.Marks.Contains(_desired);
        var reasonMissing = requiresReason && string.IsNullOrWhiteSpace(_reason.Text);
        var ownsExisting = _existing is { } old && entry.OwnedMarks.Contains(old);

        _mark.Disabled = !entry.Available || desiredExists || reasonMissing;
        _remove.Disabled = !entry.Available || _existing == null;
        _change.Disabled = !entry.Available || !ownsExisting || desiredExists || _existing == _desired || reasonMissing;

        var kindName = Loc.GetString(YautjaMarkSystem.GetMarkName(_desired));
        if (!entry.Available)
            SetStatus(Loc.GetString("cmu-yautja-mark-target-unavailable"), true);
        else if (desiredExists)
            SetStatus(Loc.GetString("cmu-yautja-mark-ui-blocked-has", ("target", entry.Name), ("kind", kindName)), true);
        else if (reasonMissing)
            SetStatus(Loc.GetString("cmu-yautja-mark-ui-blocked-reason", ("kind", kindName)), true);
        else
            SetStatus(Loc.GetString("cmu-yautja-mark-ui-ready", ("target", entry.Name), ("kind", kindName)), false);
    }

    private void SetStatus(string text, bool blocked)
    {
        Write(_status, text, blocked ? Hud.Hot : Hud.Muted);
    }

    // labels don't wrap, and the detail and status lines run long at this width
    private void Write(RichTextLabel label, string text, Color color)
    {
        var message = new Robust.Shared.Utility.FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        label.SetMessage(message, defaultColor: color);
    }

    private void SendMark()
    {
        if (Selected is not { } entry)
            return;

        OnMark?.Invoke(entry.RecordId, _revision, _desired, _reason.Text);
    }

    private void SendChange()
    {
        if (Selected is not { } entry || _existing is not { } old || !entry.OwnedMarks.Contains(old))
            return;

        OnChange?.Invoke(entry.RecordId, _revision, old, _desired, _reason.Text);
    }

    private void SendRemove()
    {
        if (Selected is not { } entry || _existing is not { } old)
            return;

        OnUnmark?.Invoke(entry.RecordId, _revision, old);
    }

    private HudTag MarkTag(YautjaMarkKind kind, bool owned)
    {
        // yours are lit solid, other hunters' are only outlined, so the difference doesn't rest on colour
        return new HudTag(Loc.GetString(YautjaMarkSystem.GetMarkName(kind)), _small)
        {
            Fill = KindColor(kind),
            Outline = !owned,
        };
    }

    private static Color KindColor(YautjaMarkKind kind)
    {
        return kind switch
        {
            YautjaMarkKind.Prey => Hud.Hot,
            YautjaMarkKind.Dishonored => Hud.Bright,
            YautjaMarkKind.Honored => Color.FromHex("#ffc46b"),
            YautjaMarkKind.GearCarrier => Color.FromHex("#ff9a6b"),
            YautjaMarkKind.Thrall => Color.FromHex("#e0866b"),
            YautjaMarkKind.Blooded => Color.FromHex("#f0b48a"),
            YautjaMarkKind.Student => Color.FromHex("#d9a06b"),
            _ => Hud.Label,
        };
    }

    private static string TypeText(YautjaMarkPanelEntry entry)
    {
        return Loc.GetString(entry.IsXeno ? "cmu-yautja-target-xeno" : "cmu-yautja-target-humanoid");
    }

    private static bool RequiresReason(YautjaMarkKind kind)
    {
        return kind is YautjaMarkKind.Thrall or YautjaMarkKind.Blooded;
    }

    private static List<YautjaMarkKind> GetRemovableMarks(YautjaMarkPanelEntry entry)
    {
        var result = new List<YautjaMarkKind>(entry.OwnedMarks);
        if (entry.Marks.Contains(YautjaMarkKind.GearCarrier) && !result.Contains(YautjaMarkKind.GearCarrier))
            result.Add(YautjaMarkKind.GearCarrier);
        return result;
    }

    private static string GetMarkKey(YautjaMarkKind kind)
    {
        return kind switch
        {
            YautjaMarkKind.Prey => "prey",
            YautjaMarkKind.Honored => "honored",
            YautjaMarkKind.Dishonored => "dishonored",
            YautjaMarkKind.GearCarrier => "gear-carrier",
            YautjaMarkKind.Thrall => "thrall",
            YautjaMarkKind.Student => "student",
            YautjaMarkKind.Blooded => "blooded",
            _ => "unknown",
        };
    }

    private Label Caption(string text, float top = 0)
    {
        var label = Hud.Write(text.ToUpperInvariant(), _small, Hud.Muted);
        label.Margin = new Thickness(0, top, 0, 0);
        return label;
    }

    private static BoxContainer HBox(int separation)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    private static BoxContainer VBox(int separation)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }
}
