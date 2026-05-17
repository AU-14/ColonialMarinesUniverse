using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaMarkWindow : DefaultWindow
{
    private readonly OptionButton _markKindOption;
    private readonly Label _summaryLabel;
    private readonly Label _selectionLabel;
    private readonly BoxContainer _targetList;
    private readonly Button _markButton;
    private readonly Button _unmarkButton;

    public event Action<NetEntity, YautjaMarkKind, string?>? OnMark;
    public event Action<NetEntity, YautjaMarkKind>? OnUnmark;

    private readonly List<YautjaMarkPanelEntry> _entries = new();
    private int? _selectedIndex;

    public YautjaMarkWindow()
    {
        Title = Loc.GetString("cmu-yautja-mark-window-title");
        SetSize = new Vector2(560, 440);
        MinSize = new Vector2(480, 340);

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

        _markKindOption = new YautjaMarkOptionButton();
        controlsBody.AddChild(_markKindOption);

        AddMarkKind(YautjaMarkKind.Prey);
        AddMarkKind(YautjaMarkKind.Honored);
        AddMarkKind(YautjaMarkKind.Dishonored);
        AddMarkKind(YautjaMarkKind.GearCarrier);
        AddMarkKind(YautjaMarkKind.Thrall);
        AddMarkKind(YautjaMarkKind.Student);
        AddMarkKind(YautjaMarkKind.Blooded);
        _markKindOption.OnItemSelected += args => _markKindOption.SelectId(args.Id);
        _markKindOption.SelectId((int) YautjaMarkKind.Prey);

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

        var actionRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        root.AddChild(actionRow);

        _selectionLabel = YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-mark-selection-none"), YautjaBracerUiStyle.Muted, "LabelSubText");
        _selectionLabel.HorizontalExpand = true;
        _selectionLabel.VerticalAlignment = Control.VAlignment.Center;
        actionRow.AddChild(_selectionLabel);

        _markButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-apply"),
            YautjaBracerUiStyle.Green);
        _markButton.OnPressed += _ => SendMark(false);
        actionRow.AddChild(_markButton);

        _unmarkButton = BuildFooterButton(
            Loc.GetString("cmu-yautja-mark-remove"),
            YautjaBracerUiStyle.HotRed);
        _unmarkButton.OnPressed += _ => SendMark(true);
        actionRow.AddChild(_unmarkButton);

        RefreshSelectionState();
    }

    public void UpdateState(YautjaMarkPanelState state)
    {
        NetEntity? previousSelection = null;
        if (_selectedIndex is { } selected &&
            selected >= 0 &&
            selected < _entries.Count)
        {
            previousSelection = _entries[selected].Entity;
        }

        _entries.Clear();
        _entries.AddRange(state.Entries);
        _selectedIndex = null;

        if (previousSelection is { } previous)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Entity != previous)
                    continue;

                _selectedIndex = i;
                break;
            }
        }

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

    private static Button BuildFooterButton(string title, Color accent)
    {
        var button = new Button
        {
            HorizontalExpand = false,
            MinWidth = 104,
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

    private void RebuildTargets()
    {
        _targetList.RemoveAllChildren();
        _summaryLabel.Text = Loc.GetString("cmu-yautja-mark-target-summary", ("count", _entries.Count));

        if (_entries.Count == 0)
        {
            _targetList.AddChild(YautjaBracerUiStyle.Empty(Loc.GetString("cmu-yautja-mark-no-targets")));
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
        _markButton.Disabled = !hasSelection;
        _unmarkButton.Disabled = !hasSelection;
        _selectionLabel.Text = hasSelection
            ? Loc.GetString("cmu-yautja-mark-selection", ("target", _entries[_selectedIndex!.Value].Name))
            : Loc.GetString("cmu-yautja-mark-selection-none");
    }

    private void SendMark(bool remove)
    {
        if (_selectedIndex is not { } selected || selected < 0 || selected >= _entries.Count)
            return;

        var entry = _entries[selected];
        var kind = (YautjaMarkKind) _markKindOption.SelectedId;
        if (remove)
            OnUnmark?.Invoke(entry.Entity, kind);
        else
            OnMark?.Invoke(entry.Entity, kind, null);
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
