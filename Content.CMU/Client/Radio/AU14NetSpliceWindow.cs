using System.Collections.Generic;
using System.Numerics;
using Content.Shared._AU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._AU14.Radio;

/// <summary>
///     The splice panel, laid out as the hand-built band scope the kit's description says it is. Built in
///     code rather than XAML because the scope and the detection meter are custom-drawn controls.
///
///     Colours follow the AN/PRC panel: dark chassis, green phosphor readouts, blue-grey chrome.
/// </summary>
public sealed class AU14NetSpliceWindow : DefaultWindow
{
    private static readonly Color Chassis = Color.FromHex("#0E0F13");
    private static readonly Color PanelFill = Color.FromHex("#181A20");
    private static readonly Color PanelBorder = Color.FromHex("#343846");
    private static readonly Color ScopeBorder = Color.FromHex("#245236");
    private static readonly Color ScopeFill = Color.FromHex("#07130A");
    private static readonly Color Chrome = Color.FromHex("#C8D2E8");
    private static readonly Color Muted = Color.FromHex("#687A9A");
    private static readonly Color Phosphor = Color.FromHex("#4A7A55");
    private static readonly Color PhosphorBright = Color.FromHex("#8FE86B");
    private static readonly Color Danger = Color.FromHex("#C4453C");
    private static readonly Color Caution = Color.FromHex("#D69B32");
    private static readonly Color Calm = Color.FromHex("#4FCB6B");

    public event Action<int>? OnProbe;
    public event Action<int>? OnLock;

    private readonly AU14NetSpliceBandDisplay _band;
    private readonly AU14NetSpliceMeter _meter;
    private readonly Slider _tuner;
    private readonly Label _stageLabel;
    private readonly Label _probesLabel;
    private readonly Label _detectionLabel;
    private readonly Label _positionLabel;
    private readonly Label _readingLabel;
    private readonly Label _statusLabel;
    private readonly Button _probeButton;
    private readonly Button _lockButton;

    private int _bandSize = 100;
    private float _statusBlink;
    private bool _warning;

    public AU14NetSpliceWindow()
    {
        Title = Loc.GetString("au14-splice-window-title");
        MinSize = new Vector2(500, 500);
        SetSize = new Vector2(520, 560);

        var chassis = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Chassis },
        };

        AddChild(chassis);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 5,
        };

        chassis.AddChild(root);

        // ----- header --------------------------------------------------------------------------------

        _stageLabel = new Label { FontColorOverride = PhosphorBright };
        _probesLabel = new Label { FontColorOverride = Muted, HorizontalAlignment = HAlignment.Right };

        var headerLeft = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 1,
        };

        headerLeft.AddChild(new Label { Text = "FEEDER JUNCTION", FontColorOverride = Chrome });
        headerLeft.AddChild(new Label { Text = "TRUNK SPLICE", FontColorOverride = Muted });

        var headerRight = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 1,
        };

        headerRight.AddChild(_stageLabel);
        headerRight.AddChild(_probesLabel);

        var headerRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 4),
            SeparationOverride = 6,
        };

        headerRow.AddChild(headerLeft);
        headerRow.AddChild(headerRight);
        root.AddChild(Framed(headerRow, PanelFill, PanelBorder));

        // ----- scope ---------------------------------------------------------------------------------

        _band = new AU14NetSpliceBandDisplay();

        var scopeBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            SeparationOverride = 2,
        };

        scopeBox.AddChild(_band);
        scopeBox.AddChild(BuildScale());
        root.AddChild(Framed(scopeBox, ScopeFill, ScopeBorder, 2));

        _readingLabel = new Label
        {
            Text = Loc.GetString("au14-splice-no-reading"),
            FontColorOverride = Phosphor,
            Margin = new Thickness(2, 0),
        };

        root.AddChild(_readingLabel);

        // ----- detection -----------------------------------------------------------------------------

        _detectionLabel = new Label { FontColorOverride = Calm, HorizontalExpand = true };
        _statusLabel = new Label { FontColorOverride = Danger, HorizontalAlignment = HAlignment.Right };

        var detectionHeader = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };

        detectionHeader.AddChild(_detectionLabel);
        detectionHeader.AddChild(_statusLabel);

        _meter = new AU14NetSpliceMeter();

        var detectionBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6, 5),
            SeparationOverride = 4,
        };

        detectionBox.AddChild(detectionHeader);
        detectionBox.AddChild(_meter);
        root.AddChild(Framed(detectionBox, PanelFill, PanelBorder));

        // ----- tuner ---------------------------------------------------------------------------------

        _positionLabel = new Label { FontColorOverride = PhosphorBright };

        _tuner = new Slider
        {
            MinValue = 1f,
            MaxValue = _bandSize,
            Value = 1f,
            Rounded = true,
            HorizontalExpand = true,
        };

        _tuner.OnValueChanged += _ => UpdateCursor();

        // the lock tolerance is three positions wide, so the slider alone is not enough to aim with
        var nudges = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 3,
        };

        AddNudge(nudges, "<<", -10);
        AddNudge(nudges, "<", -1);
        AddNudge(nudges, ">", 1);
        AddNudge(nudges, ">>", 10);

        var tunerBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6, 5),
            SeparationOverride = 4,
        };

        tunerBox.AddChild(_positionLabel);
        tunerBox.AddChild(_tuner);
        tunerBox.AddChild(nudges);
        root.AddChild(Framed(tunerBox, PanelFill, PanelBorder));

        // ----- actions -------------------------------------------------------------------------------

        _probeButton = new Button
        {
            Text = Loc.GetString("au14-splice-probe"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 4, 0),
        };

        _lockButton = new Button
        {
            Text = Loc.GetString("au14-splice-lock"),
            HorizontalExpand = true,
        };

        _probeButton.OnPressed += _ => OnProbe?.Invoke(CurrentPosition);
        _lockButton.OnPressed += _ => OnLock?.Invoke(CurrentPosition);

        var actions = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        actions.AddChild(_probeButton);
        actions.AddChild(_lockButton);
        root.AddChild(actions);

        var help = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(2, 4, 2, 0) };
        help.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("au14-splice-help")));
        root.AddChild(help);

        UpdateCursor();
    }

    private int CurrentPosition => (int) MathF.Round(_tuner.Value);

    private static PanelContainer Framed(Control child, Color fill, Color border, float thickness = 1f)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = fill,
                BorderColor = border,
                BorderThickness = new Thickness(thickness),
            },
        };

        panel.AddChild(child);

        return panel;
    }

    /// <summary>Numeric scale under the scope. Labels rather than drawn text, so the scope needs no font.</summary>
    private Control BuildScale()
    {
        var scale = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        for (var i = 0; i <= 4; i++)
        {
            scale.AddChild(new Label
            {
                Text = (i * 25).ToString(),
                FontColorOverride = Phosphor,
                HorizontalAlignment = i switch
                {
                    0 => HAlignment.Left,
                    4 => HAlignment.Right,
                    _ => HAlignment.Center,
                },
                HorizontalExpand = true,
            });
        }

        return scale;
    }

    private void AddNudge(Control parent, string text, int delta)
    {
        var button = new Button { Text = text, HorizontalExpand = true };

        button.OnPressed += _ => _tuner.Value = Math.Clamp(CurrentPosition + delta, 1, _bandSize);
        parent.AddChild(button);
    }

    private void UpdateCursor()
    {
        _band.SetCursor(CurrentPosition);
        _positionLabel.Text = Loc.GetString("au14-splice-position", ("position", CurrentPosition));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_warning)
            return;

        // the warning line blinks in step with the meter's lit end
        _statusBlink += args.DeltaSeconds;
        _statusLabel.Visible = MathF.Sin(_statusBlink * 8f) > 0f;
    }

    public void UpdateState(AU14NetSpliceBuiState state)
    {
        if (state.BandSize != _bandSize)
        {
            _bandSize = Math.Max(1, state.BandSize);
            _tuner.MaxValue = _bandSize;
        }

        _band.SetBand(state.BandSize, state.Readings, state.Locked);

        _stageLabel.Text = Loc.GetString("au14-splice-stage",
            ("current", Math.Min(state.Stage + 1, state.Carriers)),
            ("total", state.Carriers));

        _probesLabel.Text = Loc.GetString("au14-splice-probes", ("probes", state.ProbesLeft));

        _detectionLabel.Text = Loc.GetString("au14-splice-detection", ("percent", (int) state.Detection));
        _meter.SetValue(state.Detection);

        _detectionLabel.FontColorOverride = state.Detection switch
        {
            >= 70f => Danger,
            >= 40f => Caution,
            _ => Calm,
        };

        _warning = state.Detection >= 70f;
        _statusLabel.Text = _warning ? Loc.GetString("au14-splice-warning") : string.Empty;
        _statusLabel.Visible = _warning;

        _readingLabel.Text = state.Readings.Count == 0
            ? Loc.GetString("au14-splice-no-reading")
            : Loc.GetString("au14-splice-reading",
                ("position", state.Readings[^1].Position),
                ("strength", state.Readings[^1].Strength));

        _readingLabel.FontColorOverride = state.Readings.Count > 0 && state.Readings[^1].Strength > 0
            ? PhosphorBright
            : Phosphor;

        var running = state.Status == AU14NetSpliceStatus.Running;

        _probeButton.Disabled = !running || state.ProbesLeft <= 0;
        _lockButton.Disabled = !running;

        UpdateCursor();
    }
}
