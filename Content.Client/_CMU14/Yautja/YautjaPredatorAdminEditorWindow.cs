using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaPredatorAdminEditorWindow : DefaultWindow
{
    private readonly Label _roundLabel;
    private readonly Label _huntLabel;
    private readonly Label _activeSlotsLabel;
    private readonly Label _randomRoundsLabel;
    private readonly Label _statusLabel;
    private readonly SpinBox _hunterSlots;
    private readonly SpinBox _randomMinimumRounds;
    private readonly SpinBox _randomMaximumRounds;
    private readonly CheckBox _randomEnabled;
    private readonly Button _initializeButton;

    public event Action? OnInitialize;
    public event Action<int>? OnHunterSlotsChanged;
    public event Action<bool, int, int>? OnRandomChanged;
    public event Action? OnRefresh;

    public YautjaPredatorAdminEditorWindow()
    {
        Title = Loc.GetString("cmu-yautja-admin-editor-title");
        SetSize = MinSize = new Vector2(590, 460);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        Contents.AddChild(root);

        _roundLabel = new Label { HorizontalExpand = true };
        root.AddChild(_roundLabel);

        _huntLabel = new Label { HorizontalExpand = true };
        root.AddChild(_huntLabel);

        _initializeButton = new Button
        {
            Text = Loc.GetString("cmu-yautja-admin-editor-initialize"),
            HorizontalExpand = true,
        };
        _initializeButton.OnPressed += _ => OnInitialize?.Invoke();
        root.AddChild(_initializeButton);

        root.AddChild(CreateHeader("cmu-yautja-admin-editor-slots-header"));

        var slotsRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        slotsRow.AddChild(new Label
        {
            Text = Loc.GetString("cmu-yautja-admin-editor-slots"),
            MinWidth = 220,
            VerticalAlignment = VAlignment.Center,
        });
        _hunterSlots = CreateSpinBox(1, 50, 3);
        slotsRow.AddChild(_hunterSlots);
        var applySlotsButton = new Button
        {
            Text = Loc.GetString("cmu-yautja-admin-editor-apply-slots"),
            MinWidth = 150,
        };
        applySlotsButton.OnPressed += _ => OnHunterSlotsChanged?.Invoke(_hunterSlots.Value);
        slotsRow.AddChild(applySlotsButton);
        root.AddChild(slotsRow);

        _activeSlotsLabel = new Label { HorizontalExpand = true };
        root.AddChild(_activeSlotsLabel);

        root.AddChild(CreateHeader("cmu-yautja-admin-editor-random-header"));

        _randomEnabled = new CheckBox
        {
            Text = Loc.GetString("cmu-yautja-admin-editor-random-enable"),
        };
        root.AddChild(_randomEnabled);

        var minimumRow = CreateSpinBoxRow(
            "cmu-yautja-admin-editor-random-min",
            out _randomMinimumRounds,
            1,
            100,
            2);
        root.AddChild(minimumRow);

        var maximumRow = CreateSpinBoxRow(
            "cmu-yautja-admin-editor-random-max",
            out _randomMaximumRounds,
            1,
            100,
            6);
        root.AddChild(maximumRow);

        var randomButtonRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        var applyRandomButton = new Button
        {
            Text = Loc.GetString("cmu-yautja-admin-editor-apply-random"),
            HorizontalExpand = true,
        };
        applyRandomButton.OnPressed += _ => OnRandomChanged?.Invoke(
            _randomEnabled.Pressed,
            _randomMinimumRounds.Value,
            _randomMaximumRounds.Value);
        randomButtonRow.AddChild(applyRandomButton);
        var refreshButton = new Button
        {
            Text = Loc.GetString("cmu-yautja-admin-editor-refresh"),
            MinWidth = 110,
        };
        refreshButton.OnPressed += _ => OnRefresh?.Invoke();
        randomButtonRow.AddChild(refreshButton);
        root.AddChild(randomButtonRow);

        _randomRoundsLabel = new Label { HorizontalExpand = true };
        root.AddChild(_randomRoundsLabel);

        _statusLabel = new Label { HorizontalExpand = true };
        root.AddChild(_statusLabel);
    }

    public void UpdateState(YautjaPredatorAdminEditorEuiState state)
    {
        var roundStatus = Loc.GetString(state.RoundActive
            ? "cmu-yautja-admin-editor-round-active"
            : "cmu-yautja-admin-editor-round-lobby");
        _roundLabel.Text = Loc.GetString(
            "cmu-yautja-admin-editor-round-status",
            ("round", state.RoundId),
            ("status", roundStatus));
        _huntLabel.Text = Loc.GetString(state.HuntInitialized
            ? "cmu-yautja-admin-editor-hunt-initialized-state"
            : "cmu-yautja-admin-editor-hunt-not-initialized");
        _initializeButton.Disabled = !state.RoundActive;
        _activeSlotsLabel.Text = Loc.GetString(
            "cmu-yautja-admin-editor-slots-current",
            ("active", state.ActiveHunterSlots),
            ("configured", state.HunterSlots));

        _hunterSlots.OverrideValue(Math.Clamp(state.HunterSlots, 1, 50));
        _randomEnabled.Pressed = state.RandomEnabled;
        _randomMinimumRounds.OverrideValue(Math.Clamp(state.RandomMinimumRounds, 1, 100));
        _randomMaximumRounds.OverrideValue(Math.Clamp(state.RandomMaximumRounds, 1, 100));
        _randomRoundsLabel.Text = Loc.GetString(
            "cmu-yautja-admin-editor-random-remaining",
            ("rounds", state.RandomRoundsRemaining));
        _statusLabel.Text = state.StatusMessage;
    }

    private static Label CreateHeader(string locId)
    {
        return new Label
        {
            Text = Loc.GetString(locId),
            FontColorOverride = Color.LightGray,
            HorizontalExpand = true,
        };
    }

    private static BoxContainer CreateSpinBoxRow(
        string labelLocId,
        out SpinBox spinBox,
        int minimum,
        int maximum,
        int value)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        row.AddChild(new Label
        {
            Text = Loc.GetString(labelLocId),
            MinWidth = 220,
            VerticalAlignment = VAlignment.Center,
        });
        spinBox = CreateSpinBox(minimum, maximum, value);
        row.AddChild(spinBox);
        return row;
    }

    private static SpinBox CreateSpinBox(int minimum, int maximum, int value)
    {
        var spinBox = new SpinBox
        {
            HorizontalExpand = true,
            IsValid = input => input >= minimum && input <= maximum,
        };
        spinBox.OverrideValue(value);
        spinBox.InitDefaultButtons();
        return spinBox;
    }
}
