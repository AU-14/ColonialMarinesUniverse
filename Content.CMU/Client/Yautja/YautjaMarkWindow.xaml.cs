using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Yautja;

public sealed class YautjaMarkWindow : DefaultWindow
{
    private readonly OptionButton _markKindOption;
    private readonly OptionButton _oldKindOption;
    private readonly Label _summaryLabel;
    private readonly Label _selectionLabel;
    private readonly Label _selectionMarksLabel;
    private readonly Label _markKindHelpLabel;
    private readonly Label _viewDescriptionLabel;
    private readonly Label _pageLabel;
    private readonly Label _noOwnedMarkLabel;
    private readonly BoxContainer _targetList;
    private readonly BoxContainer _manageMarkControls;
    private readonly Button _recentButton;
    private readonly Button _historyButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _markButton;
    private readonly Button _unmarkButton;
    private readonly Button _changeButton;

    public event Action<int, uint, YautjaMarkKind, string?>? OnMark;
    public event Action<int, uint, YautjaMarkKind>? OnUnmark;
    public event Action<int, uint, YautjaMarkKind, YautjaMarkKind, string?>? OnChange;
    public event Action<bool, int>? OnRefresh;

    private readonly List<YautjaMarkPanelEntry> _entries = new();
    private int? _selectedIndex;
    private uint _revision;
    private bool _history;
    private int _page;
    private int _pages = 1;

    public YautjaMarkWindow()
    {
        Title = Loc.GetString("cmu-yautja-mark-window-title");
        SetSize = new Vector2(700, 600);
        MinSize = new Vector2(620, 520);

        var rootPanel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.Surface, YautjaBracerUiStyle.Border, new Thickness(2));
        AddChild(rootPanel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 9,
            Margin = new Thickness(12),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rootPanel.AddChild(root);

        root.AddChild(BuildHeader());

        var controls = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-mark-section-command"), out var controlsBody, YautjaBracerUiStyle.HotRed);
        controlsBody.VerticalExpand = false;
        root.AddChild(controls);

        controlsBody.AddChild(YautjaBracerUiStyle.Label(
            Loc.GetString("cmu-yautja-mark-view-instruction"),
            YautjaBracerUiStyle.Muted,
            "LabelSubText"));

        var viewRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        controlsBody.AddChild(viewRow);
        _recentButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-view-recent"),
            YautjaBracerUiStyle.Amber,
            150);
        _recentButton.OnPressed += _ => OnRefresh?.Invoke(false, 0);
        viewRow.AddChild(_recentButton);

        _historyButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-view-history"),
            YautjaBracerUiStyle.Amber,
            150);
        _historyButton.OnPressed += _ => OnRefresh?.Invoke(true, 0);
        viewRow.AddChild(_historyButton);
        viewRow.AddChild(new Control { HorizontalExpand = true });

        _previousButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-previous"),
            YautjaBracerUiStyle.Muted,
            92);
        _previousButton.OnPressed += _ => OnRefresh?.Invoke(true, Math.Max(0, _page - 1));
        viewRow.AddChild(_previousButton);

        _pageLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Text, "LabelSubText");
        _pageLabel.MinWidth = 72;
        _pageLabel.HorizontalAlignment = Control.HAlignment.Center;
        _pageLabel.VerticalAlignment = Control.VAlignment.Center;
        viewRow.AddChild(_pageLabel);

        _nextButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-next"),
            YautjaBracerUiStyle.Muted,
            72);
        _nextButton.OnPressed += _ => OnRefresh?.Invoke(true, Math.Min(_pages - 1, _page + 1));
        viewRow.AddChild(_nextButton);

        _viewDescriptionLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Dim, "LabelSubText");
        controlsBody.AddChild(_viewDescriptionLabel);

        var targetPanel = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-mark-section-targets"), out var targetBody, YautjaBracerUiStyle.Amber);
        targetPanel.VerticalExpand = true;
        root.AddChild(targetPanel);

        _summaryLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Muted, "LabelSubText");
        targetBody.AddChild(_summaryLabel);

        var scrollFrame = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder);
        scrollFrame.VerticalExpand = true;
        targetBody.AddChild(scrollFrame);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(7),
        };
        scrollFrame.AddChild(scroll);

        _targetList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_targetList);

        var selectionPanel = YautjaBracerUiStyle.Section(
            Loc.GetString("cmu-yautja-mark-section-selection"),
            out var selectionBody,
            YautjaBracerUiStyle.Green);
        selectionBody.VerticalExpand = false;
        root.AddChild(selectionPanel);

        _selectionLabel = YautjaBracerUiStyle.Label(
            Loc.GetString("cmu-yautja-mark-selection-none"),
            YautjaBracerUiStyle.Text,
            "LabelKeyText");
        selectionBody.AddChild(_selectionLabel);

        _selectionMarksLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Muted, "LabelSubText");
        selectionBody.AddChild(_selectionMarksLabel);

        var applyRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        selectionBody.AddChild(applyRow);

        _markKindOption = new YautjaMarkOptionButton();
        applyRow.AddChild(BuildOptionField(
            Loc.GetString("cmu-yautja-mark-desired-kind"),
            _markKindOption));

        _markButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-apply"),
            YautjaBracerUiStyle.Green,
            150);
        _markButton.VerticalAlignment = Control.VAlignment.Bottom;
        _markButton.OnPressed += _ => SendMark(false);
        applyRow.AddChild(_markButton);

        _markKindHelpLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Dim, "LabelSubText");
        selectionBody.AddChild(_markKindHelpLabel);

        _manageMarkControls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        selectionBody.AddChild(_manageMarkControls);

        _oldKindOption = new YautjaMarkOptionButton();
        _oldKindOption.OnItemSelected += args =>
        {
            _oldKindOption.SelectId(args.Id);
            RefreshActionButtons();
        };
        _manageMarkControls.AddChild(BuildOptionField(
            Loc.GetString("cmu-yautja-mark-existing-kind"),
            _oldKindOption));

        _unmarkButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-remove"),
            YautjaBracerUiStyle.HotRed,
            160);
        _unmarkButton.VerticalAlignment = Control.VAlignment.Bottom;
        _unmarkButton.OnPressed += _ => SendMark(true);
        _manageMarkControls.AddChild(_unmarkButton);

        _changeButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-change"),
            YautjaBracerUiStyle.Amber,
            160);
        _changeButton.VerticalAlignment = Control.VAlignment.Bottom;
        _changeButton.OnPressed += _ => SendChange();
        _manageMarkControls.AddChild(_changeButton);

        _noOwnedMarkLabel = YautjaBracerUiStyle.Label(
            Loc.GetString("cmu-yautja-mark-no-owned-mark"),
            YautjaBracerUiStyle.Dim,
            "LabelSubText");
        selectionBody.AddChild(_noOwnedMarkLabel);

        AddMarkKind(YautjaMarkKind.Prey);
        AddMarkKind(YautjaMarkKind.Honored);
        AddMarkKind(YautjaMarkKind.Dishonored);
        AddMarkKind(YautjaMarkKind.GearCarrier);
        AddMarkKind(YautjaMarkKind.Thrall);
        AddMarkKind(YautjaMarkKind.Student);
        AddMarkKind(YautjaMarkKind.Blooded);
        _markKindOption.OnItemSelected += args =>
        {
            _markKindOption.SelectId(args.Id);
            RefreshMarkKindHelp();
        };
        _markKindOption.SelectId((int) YautjaMarkKind.Prey);

        RefreshMarkKindHelp();
        RefreshSelectionState();
    }

    public void UpdateState(YautjaMarkPanelState state)
    {
        int? previousSelection = null;
        if (_selectedIndex is { } selected &&
            selected >= 0 &&
            selected < _entries.Count)
        {
            previousSelection = _entries[selected].RecordId;
        }

        _entries.Clear();
        _entries.AddRange(state.Entries);
        _selectedIndex = null;

        if (previousSelection is { } previous)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].RecordId != previous)
                    continue;

                _selectedIndex = i;
                break;
            }
        }

        _revision = state.Revision;
        _history = state.History;
        _page = state.Page;
        _pages = state.Pages;

        RebuildTargets();
        RefreshSelectionState();
    }

    private Control BuildHeader()
    {
        var panel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder);
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(9, 6),
            HorizontalExpand = true,
        };
        panel.AddChild(root);
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        root.AddChild(row);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        row.AddChild(text);
        text.AddChild(YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-mark-window-title"), YautjaBracerUiStyle.HotRed, "LabelHeading"));
        text.AddChild(YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-mark-window-subtitle"), YautjaBracerUiStyle.Muted, "LabelSubText"));

        var close = YautjaBracerUiStyle.CloseButton();
        close.OnPressed += _ => Close();
        row.AddChild(close);

        return panel;
    }

    private static Button BuildFooterButton(string title, Color accent, float minWidth = 104)
    {
        var button = new Button
        {
            HorizontalExpand = false,
            MinWidth = minWidth,
            MinHeight = 38,
            SetHeight = 38,
            StyleBoxOverride = YautjaBracerUiStyle.Flat(Color.Transparent, Color.Transparent, new Thickness(0)),
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            Margin = new Thickness(7, 5),
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 24),
            PanelOverride = YautjaBracerUiStyle.Flat(accent, accent),
        });

        var label = YautjaBracerUiStyle.Label(title, YautjaBracerUiStyle.Text, "LabelKeyText");
        label.VerticalAlignment = Control.VAlignment.Center;
        label.HorizontalExpand = true;
        row.AddChild(label);

        var panel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.DeepCard, accent);
        panel.AddChild(row);
        button.AddChild(panel);
        return button;
    }

    private static Control BuildOptionField(string title, OptionButton option)
    {
        var field = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
        };
        field.AddChild(YautjaBracerUiStyle.Label(title, YautjaBracerUiStyle.Muted, "LabelSubText"));
        field.AddChild(option);
        return field;
    }

    private void RebuildTargets()
    {
        _targetList.RemoveAllChildren();
        _recentButton.Disabled = !_history;
        _historyButton.Disabled = _history;
        _previousButton.Visible = _history;
        _pageLabel.Visible = _history;
        _nextButton.Visible = _history;
        _previousButton.Disabled = !_history || _page <= 0;
        _nextButton.Disabled = !_history || _page >= _pages - 1;
        _pageLabel.Text = Loc.GetString("cmu-yautja-mark-page", ("page", _page + 1), ("pages", _pages));
        _viewDescriptionLabel.Text = Loc.GetString(_history
            ? "cmu-yautja-mark-view-history-detail"
            : "cmu-yautja-mark-view-recent-detail");
        _summaryLabel.Text = Loc.GetString(_history ? "cmu-yautja-mark-history-summary" : "cmu-yautja-mark-target-summary",
            ("count", _entries.Count), ("page", _page + 1), ("pages", _pages));

        if (_entries.Count == 0)
        {
            _targetList.AddChild(YautjaBracerUiStyle.Empty(Loc.GetString(_history
                ? "cmu-yautja-mark-no-history"
                : "cmu-yautja-mark-no-targets")));
            return;
        }

        for (var i = 0; i < _entries.Count; i++)
            _targetList.AddChild(BuildTargetCard(i, _entries[i]));
    }

    private Control BuildTargetCard(int index, YautjaMarkPanelEntry entry)
    {
        var selected = _selectedIndex == index;
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 56,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(7, 6),
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 38),
            PanelOverride = YautjaBracerUiStyle.Flat(selected ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.Amber, selected ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.Amber),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(YautjaBracerUiStyle.Label(entry.Name, selected ? YautjaBracerUiStyle.Text : YautjaBracerUiStyle.Muted, "LabelKeyText"));
        text.AddChild(YautjaBracerUiStyle.Label(
            Loc.GetString("cmu-yautja-mark-target-detail",
                ("type", entry.IsXeno ? Loc.GetString("cmu-yautja-target-xeno") : Loc.GetString("cmu-yautja-target-humanoid")),
                ("marks", entry.Marks.Count == 0 ? Loc.GetString("cmu-yautja-mark-none") : GetMarkList(entry.Marks))),
            selected ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.Dim,
            "LabelSubText"));

        var panel = YautjaBracerUiStyle.Panel(selected ? YautjaBracerUiStyle.Row : YautjaBracerUiStyle.DeepCard, selected ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.MutedBorder);
        panel.AddChild(row);
        button.AddChild(panel);
        button.OnPressed += _ =>
        {
            _selectedIndex = index;
            RebuildTargets();
            RefreshSelectionState();
        };

        return button;
    }

    private void RefreshSelectionState()
    {
        var hasSelection = _selectedIndex is { } selected && selected >= 0 && selected < _entries.Count;
        var entry = hasSelection ? _entries[_selectedIndex!.Value] : null;
        var hasOwnedMark = entry is { OwnedMarks.Count: > 0 };
        _oldKindOption.Clear();
        if (entry != null)
        {
            foreach (var kind in entry.OwnedMarks)
                _oldKindOption.AddItem(Loc.GetString(YautjaMarkSystem.GetMarkName(kind)), (int) kind);
            if (entry.OwnedMarks.Count > 0)
                _oldKindOption.SelectId((int) entry.OwnedMarks[0]);
        }
        _selectionLabel.Text = hasSelection
            ? Loc.GetString("cmu-yautja-mark-selection", ("target", entry!.Name))
            : Loc.GetString("cmu-yautja-mark-selection-none");
        _selectionMarksLabel.Text = entry == null
            ? Loc.GetString("cmu-yautja-mark-selection-instruction")
            : Loc.GetString("cmu-yautja-mark-known-marks",
                ("marks", entry.Marks.Count == 0 ? Loc.GetString("cmu-yautja-mark-none") : GetMarkList(entry.Marks)),
                ("status", entry.Available
                    ? Loc.GetString("cmu-yautja-mark-target-available")
                    : Loc.GetString("cmu-yautja-mark-target-unavailable")));
        _manageMarkControls.Visible = hasOwnedMark;
        _noOwnedMarkLabel.Visible = hasSelection && !hasOwnedMark;
        RefreshActionButtons();
    }

    private void RefreshActionButtons()
    {
        var hasSelection = _selectedIndex is { } selected && selected >= 0 && selected < _entries.Count;
        var entry = hasSelection ? _entries[_selectedIndex!.Value] : null;
        var hasOwnedMark = entry is { OwnedMarks.Count: > 0 };
        var desiredKind = (YautjaMarkKind) _markKindOption.SelectedId;
        var desiredAlreadyExists = entry?.Marks.Contains(desiredKind) == true;
        var sameKind = hasOwnedMark && (YautjaMarkKind) _oldKindOption.SelectedId == desiredKind;

        _markButton.Disabled = entry is not { Available: true } || desiredAlreadyExists;
        _unmarkButton.Disabled = !hasOwnedMark || entry is not { Available: true };
        _changeButton.Disabled = !hasOwnedMark || entry is not { Available: true } ||
                                 desiredAlreadyExists || sameKind;
    }

    private void RefreshMarkKindHelp()
    {
        _markKindHelpLabel.Text = Loc.GetString($"cmu-yautja-mark-{GetMarkKey((YautjaMarkKind) _markKindOption.SelectedId)}-detail");
        RefreshActionButtons();
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

    private void SendMark(bool remove)
    {
        if (_selectedIndex is not { } selected || selected < 0 || selected >= _entries.Count)
            return;

        var entry = _entries[selected];
        var kind = (YautjaMarkKind) _markKindOption.SelectedId;
        if (remove)
        {
            if (entry.OwnedMarks.Count > 0)
                OnUnmark?.Invoke(entry.RecordId, _revision, (YautjaMarkKind) _oldKindOption.SelectedId);
        }
        else
            OnMark?.Invoke(entry.RecordId, _revision, kind, null);
    }

    private void SendChange()
    {
        if (_selectedIndex is not { } selected || selected < 0 || selected >= _entries.Count ||
            _entries[selected].OwnedMarks.Count == 0)
            return;
        var entry = _entries[selected];
        OnChange?.Invoke(entry.RecordId, _revision, (YautjaMarkKind) _oldKindOption.SelectedId,
            (YautjaMarkKind) _markKindOption.SelectedId, null);
    }

    private void AddMarkKind(YautjaMarkKind kind)
    {
        _markKindOption.AddItem(Loc.GetString(YautjaMarkSystem.GetMarkName(kind)), (int) kind);
    }

    private static string GetMarkList(List<YautjaMarkKind> marks)
    {
        var names = new string[marks.Count];
        for (var i = 0; i < marks.Count; i++)
            names[i] = Loc.GetString(YautjaMarkSystem.GetMarkName(marks[i]));

        return string.Join(", ", names);
    }

    private sealed class YautjaMarkOptionButton : OptionButton
    {
        public YautjaMarkOptionButton()
        {
            HorizontalExpand = true;
            MinHeight = 34;
            SetHeight = 34;
            Margin = new Thickness(0, 2, 0, 0);
            StyleBoxOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.HotRed);
        }

        public override void ButtonOverride(Button button)
        {
            button.HorizontalExpand = true;
            button.MinHeight = 32;
            button.StyleBoxOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder);
        }
    }
}
