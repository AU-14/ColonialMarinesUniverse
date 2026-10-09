using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// A vehicle spray painter's colour: red, green and blue sliders, the hex code, a few service
/// colours to start from, a swatch of the result and the paint left.
/// </summary>
public sealed class CMUVehicleSprayPainterWindow : DefaultWindow
{
    // Factory olive, desert tan, woodland green, navy blue, urban grey, black, winter white, red.
    private static readonly Color[] Presets =
    [
        Color.FromHex("#767E50"), Color.FromHex("#B49A6A"), Color.FromHex("#4E5B3A"), Color.FromHex("#3B4A6B"),
        Color.FromHex("#6E7072"), Color.FromHex("#2E2E2E"), Color.FromHex("#D8D8D0"), Color.FromHex("#8E2A22"),
    ];

    private static readonly string[] Channels = ["R", "G", "B"];

    public event Action<Color>? OnColorPicked;

    private readonly Slider[] _sliders = new Slider[3];
    private readonly Label[] _values = new Label[3];
    private readonly LineEdit _hex;
    private readonly StyleBoxFlat _swatch = new();
    private readonly Label _charges;
    private bool _updating;

    public CMUVehicleSprayPainterWindow()
    {
        Title = Loc.GetString("cmu-vehicle-spray-painter-title");
        MinSize = new Vector2(320, 0);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        Contents.AddChild(root);

        root.AddChild(new PanelContainer { PanelOverride = _swatch, MinSize = new Vector2(0, 40) });

        for (var i = 0; i < 3; i++)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            row.AddChild(new Label { Text = Channels[i], MinWidth = 14 });
            var slider = new Slider
            {
                MinValue = 0,
                MaxValue = 255,
                Rounded = true,
                HorizontalExpand = true,
            };
            slider.OnValueChanged += _ => SlidersChanged();
            _sliders[i] = slider;
            row.AddChild(slider);
            _values[i] = new Label { MinWidth = 30, Align = Label.AlignMode.Right };
            row.AddChild(_values[i]);
            root.AddChild(row);
        }

        var hexRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        hexRow.AddChild(new Label { Text = Loc.GetString("cmu-vehicle-spray-painter-hex") });
        _hex = new LineEdit { HorizontalExpand = true };
        _hex.OnTextEntered += args => HexEntered(args.Text);
        _hex.OnFocusExit += args => HexEntered(args.Text);
        hexRow.AddChild(_hex);
        root.AddChild(hexRow);

        var presets = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        foreach (var preset in Presets)
        {
            var button = new Button
            {
                MinSize = new Vector2(30, 22),
                ModulateSelfOverride = preset,
                ToolTip = preset.ToHexNoAlpha(),
            };
            button.OnPressed += _ =>
            {
                SetColor(preset);
                OnColorPicked?.Invoke(preset);
            };
            presets.AddChild(button);
        }

        root.AddChild(presets);

        _charges = new Label();
        root.AddChild(_charges);
    }

    /// <summary>
    /// Shows a colour without reporting it as picked.
    /// </summary>
    public void SetColor(Color color)
    {
        _updating = true;
        _sliders[0].SetValueWithoutEvent(MathF.Round(color.R * 255));
        _sliders[1].SetValueWithoutEvent(MathF.Round(color.G * 255));
        _sliders[2].SetValueWithoutEvent(MathF.Round(color.B * 255));
        _updating = false;
        Show(color);
    }

    public void SetCharges(int current, int max)
    {
        _charges.Text = Loc.GetString("cmu-vehicle-spray-painter-charges", ("current", current), ("max", max));
    }

    private Color SliderColor()
    {
        return new Color(_sliders[0].Value / 255f, _sliders[1].Value / 255f, _sliders[2].Value / 255f);
    }

    private void SlidersChanged()
    {
        if (_updating)
            return;

        var color = SliderColor();
        Show(color);
        OnColorPicked?.Invoke(color);
    }

    private void HexEntered(string text)
    {
        if (!Color.TryFromHex(text.StartsWith('#') ? text : $"#{text}", out var color))
        {
            _hex.Text = SliderColor().ToHexNoAlpha();
            return;
        }

        color = color.WithAlpha(1f);
        SetColor(color);
        OnColorPicked?.Invoke(color);
    }

    private void Show(Color color)
    {
        for (var i = 0; i < 3; i++)
        {
            _values[i].Text = ((int) _sliders[i].Value).ToString();
        }

        _hex.Text = color.ToHexNoAlpha();
        _swatch.BackgroundColor = color;
    }
}
