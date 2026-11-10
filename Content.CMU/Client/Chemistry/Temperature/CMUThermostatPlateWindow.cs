using System;
using System.Globalization;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Chemistry.Temperature;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Chemistry.Temperature;

// The thermostat plate window: the on/off switch on top, the target temperature (slider plus exact entry) below.
// It reads the networked CMUThermostatPlateComponent every frame, the same way the electric smelter window does.
public sealed class CMUThermostatPlateWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private EntityUid _owner;

    private readonly Label _statusLabel;
    private readonly Button _toggleButton;
    private readonly Label _targetLabel;
    private readonly Slider _targetSlider;
    private readonly LineEdit _targetEdit;

    private bool _sliderGrabbed;

    public event Action? OnTogglePressed;
    public event Action<float>? OnTargetChanged;

    public CMUThermostatPlateWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu-thermostat-window-title");
        MinSize = new Vector2(340, 200);

        var root = MachineWindowKit.Column();

        var power = MachineWindowKit.Row(12);
        _statusLabel = new Label { VerticalAlignment = Control.VAlignment.Center, HorizontalExpand = true };
        power.AddChild(_statusLabel);
        _toggleButton = new Button { MinSize = new Vector2(100, 0) };
        _toggleButton.OnPressed += _ => OnTogglePressed?.Invoke();
        power.AddChild(_toggleButton);
        root.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-thermostat-window-power"), power));

        var target = MachineWindowKit.Column(4);
        _targetLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        target.AddChild(_targetLabel);

        _targetSlider = new Slider
        {
            MinValue = 0,
            MaxValue = 1,
            Rounded = true,
            MinSize = new Vector2(300, 20),
        };
        _targetSlider.OnGrabbed += _ => _sliderGrabbed = true;
        _targetSlider.OnReleased += _ =>
        {
            _sliderGrabbed = false;
            OnTargetChanged?.Invoke(_targetSlider.Value);
        };
        target.AddChild(_targetSlider);

        var exact = MachineWindowKit.Row();
        _targetEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("cmu-thermostat-window-target-placeholder"),
        };
        _targetEdit.OnTextEntered += args => SubmitExact(args.Text);
        exact.AddChild(_targetEdit);
        var set = new Button { Text = Loc.GetString("cmu-thermostat-window-set") };
        set.OnPressed += _ => SubmitExact(_targetEdit.Text);
        exact.AddChild(set);
        target.AddChild(exact);

        root.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-thermostat-window-target"), target));

        Contents.AddChild(root);
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;

        if (_entMan.TryGetComponent<CMUThermostatPlateComponent>(owner, out var comp))
        {
            _targetSlider.MinValue = comp.MinTarget;
            _targetSlider.MaxValue = comp.MaxTarget;
            _targetSlider.SetValueWithoutEvent(comp.TargetTemperature);
        }
    }

    private void SubmitExact(string text)
    {
        if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            float.IsNaN(value) || float.IsInfinity(value))
        {
            return;
        }

        // the server clamps as well; clamping here keeps the slider and the request in agreement
        if (_entMan.TryGetComponent<CMUThermostatPlateComponent>(_owner, out var comp))
            value = Math.Clamp(value, comp.MinTarget, comp.MaxTarget);

        _targetEdit.Text = string.Empty;
        OnTargetChanged?.Invoke(value);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<CMUThermostatPlateComponent>(_owner, out var comp))
            return;

        _statusLabel.Text = Loc.GetString(comp.Enabled ? "cmu-thermostat-window-on" : "cmu-thermostat-window-off");
        _toggleButton.Text = Loc.GetString(comp.Enabled ? "cmu-thermostat-window-turn-off" : "cmu-thermostat-window-turn-on");

        _targetLabel.Text = Loc.GetString("cmu-thermostat-window-target-value",
            ("target", (int) MathF.Round(comp.TargetTemperature)),
            ("min", (int) MathF.Round(comp.MinTarget)),
            ("max", (int) MathF.Round(comp.MaxTarget)));

        if (!_sliderGrabbed)
            _targetSlider.SetValueWithoutEvent(comp.TargetTemperature);
    }
}
