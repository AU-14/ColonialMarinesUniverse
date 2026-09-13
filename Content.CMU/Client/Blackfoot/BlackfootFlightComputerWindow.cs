using System;
using System.Numerics;
using Content.Shared.CMU14.Blackfoot;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Blackfoot;

public sealed class BlackfootFlightComputerWindow : DefaultWindow
{
    private static readonly Color Surface = Color.FromHex("#0B1218");
    private static readonly Color Card = Color.FromHex("#101C24");
    private static readonly Color Border = Color.FromHex("#284252");
    private static readonly Color Text = Color.FromHex("#D9E8F2");
    private static readonly Color Muted = Color.FromHex("#8DA5B5");
    private static readonly Color Ready = Color.FromHex("#74D7A3");
    private static readonly Color Caution = Color.FromHex("#F0C15C");
    private static readonly Color Alert = Color.FromHex("#F07C72");
    private static readonly Color Fuel = Color.FromHex("#7FC8FF");
    private static readonly Color Battery = Color.FromHex("#A3DF78");

    private readonly Label _padLabel;
    private readonly Label _pumpLabel;
    private readonly Label _aircraftLabel;
    private readonly Label _fuelLabel;
    private readonly Label _batteryLabel;
    private readonly ProgressBar _fuelBar;
    private readonly ProgressBar _batteryBar;
    private readonly Button _fuelButton;
    private readonly Button _batteryButton;

    public event Action? OnFuelToggle;
    public event Action? OnBatteryToggle;

    public BlackfootFlightComputerWindow()
    {
        Title = Loc.GetString("cmu-blackfoot-flight-computer-title");
        SetSize = new Vector2(380, 260);
        MinSize = new Vector2(340, 240);
        CloseButton.Visible = true;
        CloseButton.Disabled = false;
        CloseButton.MinSize = new Vector2(18f, 18f);
        CloseButton.SetSize = new Vector2(18f, 18f);
        CloseButton.ModulateSelfOverride = Text;

        var rootPanel = Panel(Surface, Border);
        rootPanel.VerticalExpand = true;
        Contents.AddChild(rootPanel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rootPanel.AddChild(root);

        var status = Panel(Card, Border);
        root.AddChild(status);

        var statusRoot = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        status.AddChild(statusRoot);

        _padLabel = Label(Loc.GetString("cmu-blackfoot-flight-computer-pad-no-link"), Muted);
        _pumpLabel = Label(Loc.GetString("cmu-blackfoot-flight-computer-pump-no-link"), Muted);
        _aircraftLabel = Label(Loc.GetString("cmu-blackfoot-flight-computer-aircraft-none"), Muted);
        statusRoot.AddChild(_padLabel);
        statusRoot.AddChild(_pumpLabel);
        statusRoot.AddChild(_aircraftLabel);

        var fuelName = Loc.GetString("cmu-blackfoot-flight-computer-fuel");
        var batteryName = Loc.GetString("cmu-blackfoot-flight-computer-battery");
        root.AddChild(BuildMeter(fuelName, Fuel, out _fuelLabel, out _fuelBar));
        root.AddChild(BuildMeter(batteryName, Battery, out _batteryLabel, out _batteryBar));

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        root.AddChild(buttons);

        _fuelButton = ActionButton(Loc.GetString("cmu-blackfoot-flight-computer-start-refuel"), Fuel);
        _fuelButton.OnPressed += _ => OnFuelToggle?.Invoke();
        buttons.AddChild(_fuelButton);

        _batteryButton = ActionButton(Loc.GetString("cmu-blackfoot-flight-computer-start-recharge"), Battery);
        _batteryButton.OnPressed += _ => OnBatteryToggle?.Invoke();
        buttons.AddChild(_batteryButton);
    }

    public void UpdateState(BlackfootFlightComputerBuiState state)
    {
        var hasAircraft = state.Aircraft != null;

        _padLabel.Text = state.PadLinked
            ? hasAircraft
                ? Loc.GetString("cmu-blackfoot-flight-computer-pad-aircraft")
                : Loc.GetString("cmu-blackfoot-flight-computer-pad-deployed")
            : Loc.GetString("cmu-blackfoot-flight-computer-pad-none");
        _padLabel.FontColorOverride = state.PadLinked ? Ready : Alert;

        _pumpLabel.Text = state.PadLinked
            ? state.FuelPumpLinked
                ? Loc.GetString("cmu-blackfoot-flight-computer-pump-linked")
                : Loc.GetString("cmu-blackfoot-flight-computer-pump-missing")
            : Loc.GetString("cmu-blackfoot-flight-computer-pump-no-pad");
        _pumpLabel.FontColorOverride = !state.PadLinked
            ? Muted
            : state.FuelPumpLinked ? Ready : Caution;

        _aircraftLabel.Text = hasAircraft
            ? Loc.GetString("cmu-blackfoot-flight-computer-aircraft-linked")
            : Loc.GetString("cmu-blackfoot-flight-computer-aircraft-none");
        _aircraftLabel.FontColorOverride = hasAircraft ? Ready : Muted;

        UpdateMeter(
            _fuelLabel,
            _fuelBar,
            Loc.GetString("cmu-blackfoot-flight-computer-fuel"),
            state.Fuel,
            state.MaxFuel,
            hasAircraft);
        UpdateMeter(
            _batteryLabel,
            _batteryBar,
            Loc.GetString("cmu-blackfoot-flight-computer-battery"),
            state.Battery,
            state.MaxBattery,
            hasAircraft);

        _fuelButton.Text = state.Refueling
            ? Loc.GetString("cmu-blackfoot-flight-computer-stop-refuel")
            : Loc.GetString("cmu-blackfoot-flight-computer-start-refuel");
        _fuelButton.Disabled = !hasAircraft || !state.FuelPumpLinked;

        _batteryButton.Text = state.Recharging
            ? Loc.GetString("cmu-blackfoot-flight-computer-stop-recharge")
            : Loc.GetString("cmu-blackfoot-flight-computer-start-recharge");
        _batteryButton.Disabled = !hasAircraft;
    }

    private static Control BuildMeter(string title, Color accent, out Label label, out ProgressBar bar)
    {
        var panel = Panel(Card, Border);
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        panel.AddChild(root);

        label = Label(Loc.GetString("cmu-blackfoot-flight-computer-meter-empty", ("name", title)), Text);
        root.AddChild(label);

        bar = new ProgressBar
        {
            HorizontalExpand = true,
            MinHeight = 14,
            BackgroundStyleBoxOverride = Flat(Color.FromHex("#17232C"), Color.FromHex("#24343E")),
            ForegroundStyleBoxOverride = Flat(accent, accent),
        };
        root.AddChild(bar);

        return panel;
    }

    private static void UpdateMeter(Label label, ProgressBar bar, string title, float value, float max, bool active)
    {
        if (!active || max <= 0f)
        {
            label.Text = Loc.GetString("cmu-blackfoot-flight-computer-meter-empty", ("name", title));
            bar.MinValue = 0f;
            bar.MaxValue = 1f;
            bar.Value = 0f;
            return;
        }

        label.Text = Loc.GetString(
            "cmu-blackfoot-flight-computer-meter-value",
            ("name", title),
            ("value", MathF.Round(value)),
            ("max", MathF.Round(max)));
        bar.MinValue = 0f;
        bar.MaxValue = max;
        bar.Value = Math.Clamp(value, 0f, max);
    }

    private static Label Label(string text, Color color)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = color,
            ClipText = true,
            HorizontalExpand = true,
        };
    }

    private static Button ActionButton(string text, Color accent)
    {
        return new Button
        {
            Text = text,
            HorizontalExpand = true,
            MinHeight = 34,
            SetHeight = 34,
            StyleBoxOverride = Flat(Color.FromHex("#12212B"), accent),
        };
    }

    private static PanelContainer Panel(Color background, Color border)
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = Flat(background, border),
        };
    }

    private static StyleBoxFlat Flat(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1f),
        };
    }
}
