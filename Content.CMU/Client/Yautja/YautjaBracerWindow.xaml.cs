using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaBracerWindow : DefaultWindow
{
    private static readonly string[] DirectionNames =
    {
        "N",
        "NE",
        "SE",
        "S",
        "SW",
        "NW",
    };

    private Label _powerValue = default!;
    private Label _lockValue = default!;
    private Label _idValue = default!;
    private Label _coreValue = default!;
    private Label _trackerSummary = default!;
    private Label _lockButtonTitle = default!;
    private Label _lockButtonDetail = default!;
    private Label _idButtonTitle = default!;
    private Label _idButtonDetail = default!;
    private Label _selfDestructButtonTitle = default!;
    private Label _selfDestructButtonDetail = default!;
    private Label _linkThrallButtonDetail = default!;
    private Label _thrallTransmissionButtonDetail = default!;
    private Label _thrallSelfDestructButtonTitle = default!;
    private Label _thrallSelfDestructButtonDetail = default!;
    private Label _thrallLockButtonDetail = default!;
    private readonly Label[] _directionLabels = new Label[DirectionNames.Length];
    private BoxContainer _trackerList = default!;

    public Button MarksButton = default!;
    public Button TranslatorButton = default!;
    public Button LockButton = default!;
    public Button IdChipButton = default!;
    public Button SelfDestructButton = default!;
    public Button LinkThrallButton = default!;
    public Button ThrallTransmissionButton = default!;
    public Button StunThrallButton = default!;
    public Button ThrallSelfDestructButton = default!;
    public Button ThrallLockButton = default!;
    public Button CrystalButton = default!;
    public Button HumanCrystalButton = default!;
    public Button CapsuleButton = default!;
    public Button HuntingTrapButton = default!;

    public event Action<YautjaBracerPanelCommand>? OnCommand;

    public YautjaBracerWindow()
    {
        Title = Loc.GetString("cmu-yautja-bracer-menu-title");
        Resizable = true;
        SetSize = new Vector2(900, 620);
        MinSize = new Vector2(720, 480);

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
        root.AddChild(BuildHuntControlsSection());

        var main = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 9,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(main);

        var commandScroll = new ScrollContainer
        {
            MinWidth = 320,
            HorizontalExpand = false,
            VerticalExpand = true,
        };
        main.AddChild(commandScroll);

        var commandColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 9,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        commandScroll.AddChild(commandColumn);

        commandColumn.AddChild(BuildStatusSection());
        commandColumn.AddChild(BuildCommandSection());
        commandColumn.AddChild(BuildFabricatorSection());

        main.AddChild(BuildTrackerSection());

        Bind(MarksButton, YautjaBracerPanelCommand.OpenMarks);
        Bind(TranslatorButton, YautjaBracerPanelCommand.OpenTranslator);
        Bind(LockButton, YautjaBracerPanelCommand.ToggleBracerLock);
        Bind(IdChipButton, YautjaBracerPanelCommand.ToggleBracerIdChip);
        Bind(SelfDestructButton, YautjaBracerPanelCommand.ToggleSelfDestruct);
        Bind(LinkThrallButton, YautjaBracerPanelCommand.LinkThrallBracer);
        Bind(ThrallTransmissionButton, YautjaBracerPanelCommand.OpenThrallTransmission);
        Bind(StunThrallButton, YautjaBracerPanelCommand.StunThrall);
        Bind(ThrallSelfDestructButton, YautjaBracerPanelCommand.ToggleThrallSelfDestruct);
        Bind(ThrallLockButton, YautjaBracerPanelCommand.ToggleThrallBracerLock);
        Bind(CrystalButton, YautjaBracerPanelCommand.CreateStabilisingCrystal);
        Bind(HumanCrystalButton, YautjaBracerPanelCommand.CreateHumanStabilisingCrystal);
        Bind(CapsuleButton, YautjaBracerPanelCommand.CreateHealingCapsule);
        Bind(HuntingTrapButton, YautjaBracerPanelCommand.CreateHuntingTrap);
    }

    public void UpdateState(YautjaBracerPanelState state)
    {
        _powerValue.Text = Loc.GetString("cmu-yautja-bracer-menu-power-value", ("charge", state.Charge), ("max", state.MaxCharge));
        _lockValue.Text = Loc.GetString(state.Locked
            ? "cmu-yautja-bracer-menu-state-locked"
            : "cmu-yautja-bracer-menu-state-open");
        _lockValue.FontColorOverride = state.Locked ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.Green;

        _idValue.Text = Loc.GetString(state.IdChipDeployed
            ? "cmu-yautja-bracer-menu-state-deployed"
            : "cmu-yautja-bracer-menu-state-stowed");
        _idValue.FontColorOverride = state.IdChipDeployed ? YautjaBracerUiStyle.Green : YautjaBracerUiStyle.Muted;

        _coreValue.Text = Loc.GetString(state.SelfDestructArmed
            ? "cmu-yautja-bracer-menu-state-armed"
            : "cmu-yautja-bracer-menu-state-standby");
        _coreValue.FontColorOverride = state.SelfDestructArmed ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.Green;

        _lockButtonTitle.Text = Loc.GetString(state.Locked
            ? "cmu-yautja-bracer-menu-unlock"
            : "cmu-yautja-bracer-menu-toggle-lock");
        _lockButtonDetail.Text = Loc.GetString(state.Locked
            ? "cmu-yautja-bracer-menu-state-locked"
            : "cmu-yautja-bracer-menu-state-open");

        _idButtonTitle.Text = Loc.GetString("cmu-yautja-bracer-menu-toggle-id");
        _idButtonDetail.Text = Loc.GetString(state.IdChipDeployed
            ? "cmu-yautja-bracer-menu-state-deployed"
            : "cmu-yautja-bracer-menu-state-stowed");

        _selfDestructButtonTitle.Text = Loc.GetString(state.SelfDestructArmed
            ? "cmu-yautja-bracer-menu-cancel-destruct"
            : "cmu-yautja-bracer-menu-toggle-destruct");
        _selfDestructButtonDetail.Text = Loc.GetString(state.SelfDestructArmed
            ? "cmu-yautja-bracer-menu-state-armed"
            : "cmu-yautja-bracer-menu-state-standby");

        var thrallStatus = string.IsNullOrWhiteSpace(state.ThrallName)
            ? Loc.GetString("cmu-yautja-bracer-menu-thrall-none")
            : state.ThrallLinked
                ? Loc.GetString("cmu-yautja-bracer-menu-state-linked")
                : Loc.GetString("cmu-yautja-bracer-menu-state-unlinked");

        _linkThrallButtonDetail.Text = thrallStatus;
        _thrallTransmissionButtonDetail.Text = state.ThrallLinked
            ? Loc.GetString("cmu-yautja-bracer-menu-state-ready")
            : Loc.GetString("cmu-yautja-bracer-menu-state-no-signal");
        _thrallSelfDestructButtonTitle.Text = Loc.GetString(state.ThrallSelfDestructArmed
            ? "cmu-yautja-bracer-menu-cancel-thrall-destruct"
            : "cmu-yautja-bracer-menu-thrall-destruct");
        _thrallSelfDestructButtonDetail.Text = Loc.GetString(state.ThrallSelfDestructArmed
            ? "cmu-yautja-bracer-menu-state-armed"
            : "cmu-yautja-bracer-menu-state-standby");
        _thrallLockButtonDetail.Text = Loc.GetString(state.ThrallBracerLocked
            ? "cmu-yautja-bracer-menu-state-locked"
            : "cmu-yautja-bracer-menu-state-open");

        UpdateTracker(state.TrackedGear);
    }

    private Control BuildHeader()
    {
        var panel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder);
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(9, 6),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        var accent = new PanelContainer
        {
            MinSize = new Vector2(6, 42),
            PanelOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.HotRed, YautjaBracerUiStyle.HotRed),
        };
        row.AddChild(accent);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);
        text.AddChild(YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-bracer-menu-header"), YautjaBracerUiStyle.HotRed, "LabelHeading"));
        text.AddChild(YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-bracer-menu-subheader"), YautjaBracerUiStyle.Muted, "LabelSubText"));

        var close = YautjaBracerUiStyle.CloseButton();
        close.OnPressed += _ => Close();
        row.AddChild(close);

        return panel;
    }

    private Control BuildStatusSection()
    {
        var panel = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-bracer-menu-section-status"), out var body, YautjaBracerUiStyle.Red);
        var grid = new GridContainer
        {
            Columns = 1,
            HorizontalExpand = true,
        };
        body.AddChild(grid);

        _powerValue = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Text, "LabelKeyText");
        _lockValue = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Text, "LabelKeyText");
        _idValue = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Text, "LabelKeyText");
        _coreValue = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Text, "LabelKeyText");

        grid.AddChild(BuildMetricWithLabel(Loc.GetString("cmu-yautja-bracer-menu-power"), _powerValue, YautjaBracerUiStyle.HotRed));
        grid.AddChild(BuildMetricWithLabel(Loc.GetString("cmu-yautja-bracer-menu-lock"), _lockValue, YautjaBracerUiStyle.HotRed));
        grid.AddChild(BuildMetricWithLabel(Loc.GetString("cmu-yautja-bracer-menu-id"), _idValue, YautjaBracerUiStyle.Green));
        grid.AddChild(BuildMetricWithLabel(Loc.GetString("cmu-yautja-bracer-menu-destruct"), _coreValue, YautjaBracerUiStyle.Amber));
        return panel;
    }

    private Control BuildCommandSection()
    {
        var panel = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-bracer-menu-section-functions"), out var body, YautjaBracerUiStyle.HotRed);

        TranslatorButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-translator"),
            Loc.GetString("cmu-yautja-bracer-menu-translator-detail"),
            YautjaBracerUiStyle.HotRed,
            out _,
            out _);
        body.AddChild(TranslatorButton);

        LockButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-toggle-lock"),
            Loc.GetString("cmu-yautja-bracer-menu-state-locked"),
            YautjaBracerUiStyle.HotRed,
            out _lockButtonTitle,
            out _lockButtonDetail);
        body.AddChild(LockButton);

        IdChipButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-toggle-id"),
            Loc.GetString("cmu-yautja-bracer-menu-state-stowed"),
            YautjaBracerUiStyle.Green,
            out _idButtonTitle,
            out _idButtonDetail);
        body.AddChild(IdChipButton);

        SelfDestructButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-toggle-destruct"),
            Loc.GetString("cmu-yautja-bracer-menu-state-standby"),
            YautjaBracerUiStyle.Amber,
            out _selfDestructButtonTitle,
            out _selfDestructButtonDetail);
        body.AddChild(SelfDestructButton);

        return panel;
    }

    private Control BuildHuntControlsSection()
    {
        var panel = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-bracer-menu-hunt-controls"), out var body, YautjaBracerUiStyle.Green);
        body.VerticalExpand = false;

        var grid = new GridContainer
        {
            Columns = 2,
            HorizontalExpand = true,
        };
        body.AddChild(grid);

        MarksButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-marks"),
            Loc.GetString("cmu-yautja-bracer-menu-marks-detail"),
            YautjaBracerUiStyle.HotRed,
            out _,
            out _);
        grid.AddChild(MarksButton);

        LinkThrallButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-link-thrall"),
            Loc.GetString("cmu-yautja-bracer-menu-link-thrall-detail"),
            YautjaBracerUiStyle.Green,
            out _,
            out _linkThrallButtonDetail);
        grid.AddChild(LinkThrallButton);

        ThrallTransmissionButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-thrall-message"),
            Loc.GetString("cmu-yautja-bracer-menu-thrall-message-detail"),
            YautjaBracerUiStyle.Green,
            out _,
            out _thrallTransmissionButtonDetail);
        grid.AddChild(ThrallTransmissionButton);

        StunThrallButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-stun-thrall"),
            Loc.GetString("cmu-yautja-bracer-menu-stun-thrall-detail"),
            YautjaBracerUiStyle.Amber,
            out _,
            out _);
        grid.AddChild(StunThrallButton);

        ThrallSelfDestructButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-thrall-destruct"),
            Loc.GetString("cmu-yautja-bracer-menu-thrall-destruct-detail"),
            YautjaBracerUiStyle.Amber,
            out _thrallSelfDestructButtonTitle,
            out _thrallSelfDestructButtonDetail);
        grid.AddChild(ThrallSelfDestructButton);

        ThrallLockButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-toggle-thrall-lock"),
            Loc.GetString("cmu-yautja-bracer-menu-toggle-thrall-lock-detail"),
            YautjaBracerUiStyle.Green,
            out _,
            out _thrallLockButtonDetail);
        grid.AddChild(ThrallLockButton);

        return panel;
    }

    private Control BuildFabricatorSection()
    {
        var panel = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-bracer-menu-section-fabricator"), out var body, YautjaBracerUiStyle.Purple);

        CrystalButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-crystal"),
            Loc.GetString("cmu-yautja-bracer-menu-crystal-detail"),
            YautjaBracerUiStyle.Purple,
            out _,
            out _);
        body.AddChild(CrystalButton);

        HumanCrystalButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-human-crystal"),
            Loc.GetString("cmu-yautja-bracer-menu-human-crystal-detail"),
            YautjaBracerUiStyle.Purple,
            out _,
            out _);
        body.AddChild(HumanCrystalButton);

        CapsuleButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-capsule"),
            Loc.GetString("cmu-yautja-bracer-menu-capsule-detail"),
            YautjaBracerUiStyle.Purple,
            out _,
            out _);
        body.AddChild(CapsuleButton);

        HuntingTrapButton = YautjaBracerUiStyle.ActionButton(
            Loc.GetString("cmu-yautja-bracer-menu-hunting-trap"),
             Loc.GetString("cmu-yautja-bracer-menu-hunting-trap-detail"),
            YautjaBracerUiStyle.Purple,
            out _,
            out _);
        body.AddChild(HuntingTrapButton);

        return panel;
    }

    private Control BuildTrackerSection()
    {
        var panel = YautjaBracerUiStyle.Section(Loc.GetString("cmu-yautja-bracer-menu-tracker"), out var body, YautjaBracerUiStyle.HotRed);
        panel.HorizontalExpand = true;

        var summaryRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        body.AddChild(summaryRow);

        _trackerSummary = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Muted, "LabelSubText");
        _trackerSummary.HorizontalExpand = true;
        summaryRow.AddChild(_trackerSummary);

        body.AddChild(BuildDirectionStrip());

        var listFrame = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder);
        listFrame.VerticalExpand = true;
        body.AddChild(listFrame);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(7),
        };
        listFrame.AddChild(scroll);

        _trackerList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_trackerList);

        return panel;
    }

    private Control BuildDirectionStrip()
    {
        var panel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.WindowBg, YautjaBracerUiStyle.MutedBorder);
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(9, 6),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        AddDirectionReadout(row, 5);
        AddDirectionReadout(row, 0);
        AddDirectionReadout(row, 1);
        AddDirectionReadout(row, 4);
        AddDirectionReadout(row, 3);
        AddDirectionReadout(row, 2);

        return panel;
    }

    private void AddDirectionReadout(BoxContainer row, int direction)
    {
        var label = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Text, "LabelSubText");
        label.Align = Label.AlignMode.Center;
        label.HorizontalAlignment = Control.HAlignment.Center;
        label.VerticalAlignment = Control.VAlignment.Center;
        label.HorizontalExpand = true;
        label.MinWidth = 58;
        _directionLabels[direction] = label;
        row.AddChild(label);
    }

    private Control BuildMetricWithLabel(string title, Label valueLabel, Color accent)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 34),
            PanelOverride = YautjaBracerUiStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        row.AddChild(text);
        text.AddChild(YautjaBracerUiStyle.Label(title, YautjaBracerUiStyle.Muted, "LabelSubText"));
        text.AddChild(valueLabel);

        return YautjaBracerUiStyle.Wrap(row, YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder, new Thickness(7, 5));
    }

    private void UpdateTracker(List<YautjaGearTrackerEntry> trackedGear)
    {
        var counts = new int[DirectionNames.Length];
        var nearest = new int[DirectionNames.Length];
        var totalSignals = 0;
        Array.Fill(nearest, int.MaxValue);

        foreach (var entry in trackedGear)
        {
            var signalCount = Math.Max(1, entry.Count);
            totalSignals += signalCount;

            if (entry.Direction >= DirectionNames.Length)
                continue;

            counts[entry.Direction] += signalCount;
            nearest[entry.Direction] = Math.Min(nearest[entry.Direction], entry.Distance);
        }

        for (var i = 0; i < _directionLabels.Length; i++)
            _directionLabels[i].Text = FormatDirection(i, counts, nearest);

        _trackerSummary.Text = totalSignals == trackedGear.Count
            ? Loc.GetString("cmu-yautja-bracer-menu-tracker-summary", ("count", totalSignals))
            : Loc.GetString("cmu-yautja-bracer-menu-tracker-summary-grouped", ("count", totalSignals), ("locations", trackedGear.Count));
        _trackerList.RemoveAllChildren();

        if (trackedGear.Count == 0)
        {
            _trackerList.AddChild(YautjaBracerUiStyle.Empty(Loc.GetString("cmu-yautja-bracer-menu-tracker-empty")));
            return;
        }

        foreach (var entry in trackedGear)
            _trackerList.AddChild(BuildTrackerRow(entry));
    }

    private Control BuildTrackerRow(YautjaGearTrackerEntry entry)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        var direction = YautjaBracerUiStyle.Label(DirectionName(entry.Direction), YautjaBracerUiStyle.HotRed, "LabelHeading");
        direction.MinWidth = 36;
        direction.Align = Label.AlignMode.Center;
        direction.VerticalAlignment = Control.VAlignment.Center;
        row.AddChild(direction);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        row.AddChild(text);

        var title = entry.Count > 1
            ? Loc.GetString("cmu-yautja-bracer-menu-tracker-group", ("count", entry.Count))
            : entry.Name;
        var detail = entry.Count > 1
            ? Loc.GetString("cmu-yautja-bracer-menu-tracker-group-detail", ("distance", entry.Distance), ("bearing", entry.Bearing), ("signals", entry.Name))
            : Loc.GetString("cmu-yautja-bracer-menu-tracker-entry-detail", ("distance", entry.Distance), ("bearing", entry.Bearing));

        text.AddChild(YautjaBracerUiStyle.Label(title, YautjaBracerUiStyle.Text, "LabelKeyText"));
        text.AddChild(YautjaBracerUiStyle.Label(
            detail,
            YautjaBracerUiStyle.Muted,
            "LabelSubText"));

        return YautjaBracerUiStyle.Wrap(row, YautjaBracerUiStyle.Row, YautjaBracerUiStyle.MutedBorder, new Thickness(7, 5));
    }

    private static string FormatDirection(int direction, int[] counts, int[] nearest)
    {
        var range = nearest[direction] == int.MaxValue
            ? "--"
            : $"{nearest[direction]}m";

        return $"{DirectionNames[direction]}  {counts[direction]}  {range}";
    }

    private static string DirectionName(byte direction)
    {
        return direction < DirectionNames.Length ? DirectionNames[direction] : "?";
    }

    private void Bind(Button button, YautjaBracerPanelCommand command)
    {
        button.OnPressed += _ => OnCommand?.Invoke(command);
    }
}
