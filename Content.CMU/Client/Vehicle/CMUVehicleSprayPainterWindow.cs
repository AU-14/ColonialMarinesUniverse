using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// A vehicle spray painter's window. Paint: red, green and blue sliders, the hex code, a few service
/// colours to start from and a swatch of the result. Rim design: the designs on a wheel to pick from,
/// and their base, accent, lip and cap colours, the one picked mixed on the same sliders. The paint
/// left shows under both.
/// </summary>
public sealed class CMUVehicleSprayPainterWindow : DefaultWindow
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    // Factory olive, desert tan, woodland green, navy blue, urban grey, black, winter white, red.
    private static readonly Color[] Presets =
    [
        Color.FromHex("#767E50"), Color.FromHex("#B49A6A"), Color.FromHex("#4E5B3A"), Color.FromHex("#3B4A6B"),
        Color.FromHex("#6E7072"), Color.FromHex("#2E2E2E"), Color.FromHex("#D8D8D0"), Color.FromHex("#8E2A22"),
    ];

    private static readonly string[] Channels = ["R", "G", "B"];
    private static readonly string[] Regions = ["base", "accent", "lip", "cap"];
    private static readonly Color FactoryWheels = Color.FromHex("#C8CDD2");
    private const int PreviewScale = 3;

    public event Action<Color>? OnColorPicked;
    public event Action<CMUVehicleSprayPainterMode>? OnModePicked;
    public event Action<ProtoId<CMURimDesignPrototype>, List<Color>>? OnDesignPicked;

    private readonly Slider[] _sliders = new Slider[3];
    private readonly Label[] _values = new Label[3];
    private readonly LineEdit _hex;
    private readonly StyleBoxFlat _swatch = new();
    private readonly Label _charges;
    private readonly Button _paintTab;
    private readonly Button _designTab;
    private readonly Control _paintBox;
    private readonly Control _designBox;
    private readonly Dictionary<string, (Button Button, TextureRect Preview)> _designs = new();
    private readonly Dictionary<string, OwnedTexture> _textures = new();
    private readonly Button[] _regionButtons = new Button[4];
    private readonly StyleBoxFlat[] _regionSwatches = new StyleBoxFlat[4];
    private bool _updating;

    private CMUVehicleSprayPainterMode _mode;
    private Color _paint = Color.FromHex("#767E50");
    private CMURimDesignPrototype? _design;
    private readonly List<Color> _designColors = new();
    private int _region;

    public CMUVehicleSprayPainterWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("cmu-vehicle-spray-painter-title");
        MinSize = new Vector2(340, 0);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        Contents.AddChild(root);

        var tabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        var tabGroup = new ButtonGroup();
        _paintTab = new Button
        {
            Text = Loc.GetString("cmu-vehicle-spray-painter-mode-paint"),
            ToggleMode = true,
            Group = tabGroup,
            HorizontalExpand = true,
            StyleClasses = { "OpenRight" },
        };
        _designTab = new Button
        {
            Text = Loc.GetString("cmu-vehicle-spray-painter-mode-design"),
            ToggleMode = true,
            Group = tabGroup,
            HorizontalExpand = true,
            StyleClasses = { "OpenLeft" },
        };
        _paintTab.OnPressed += _ => PickMode(CMUVehicleSprayPainterMode.Paint);
        _designTab.OnPressed += _ => PickMode(CMUVehicleSprayPainterMode.Design);
        tabs.AddChild(_paintTab);
        tabs.AddChild(_designTab);
        root.AddChild(tabs);

        _paintBox = new PanelContainer { PanelOverride = _swatch, MinSize = new Vector2(0, 40) };
        root.AddChild(_paintBox);

        var design = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        _designBox = design;
        var grid = new GridContainer { Columns = 4, HSeparationOverride = 4, VSeparationOverride = 4 };
        var designGroup = new ButtonGroup();
        foreach (var rim in _prototypes.EnumeratePrototypes<CMURimDesignPrototype>().OrderBy(p => p.Order))
        {
            var preview = new TextureRect
            {
                Stretch = TextureRect.StretchMode.Scale,
                MinSize = new Vector2(19 * PreviewScale, 19 * PreviewScale),
                HorizontalAlignment = HAlignment.Center,
            };
            var tile = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            tile.AddChild(preview);
            tile.AddChild(new Label
            {
                Text = Loc.GetString(rim.Name),
                HorizontalAlignment = HAlignment.Center,
                StyleClasses = { "LabelSubText" },
            });
            var button = new Button { ToggleMode = true, Group = designGroup };
            button.AddChild(tile);
            var picked = rim;
            button.OnPressed += _ => PickDesign(picked);
            grid.AddChild(button);
            _designs[rim.ID] = (button, preview);
            preview.Texture = Preview(rim, new[] { FactoryWheels, rim.Accent, rim.LipColor, rim.CapColor });
        }

        design.AddChild(grid);

        var regions = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var regionGroup = new ButtonGroup();
        for (var i = 0; i < Regions.Length; i++)
        {
            var swatch = new StyleBoxFlat();
            _regionSwatches[i] = swatch;
            var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            content.AddChild(new PanelContainer
            {
                PanelOverride = swatch,
                MinSize = new Vector2(12, 12),
                VerticalAlignment = VAlignment.Center,
            });
            content.AddChild(new Label { Text = Loc.GetString($"cmu-vehicle-spray-painter-region-{Regions[i]}") });
            var button = new Button { ToggleMode = true, Group = regionGroup, HorizontalExpand = true };
            button.AddChild(content);
            var region = i;
            button.OnPressed += _ => PickRegion(region);
            _regionButtons[i] = button;
            regions.AddChild(button);
        }

        design.AddChild(regions);
        design.AddChild(new Label
        {
            Text = Loc.GetString("cmu-vehicle-spray-painter-design-hint"),
            StyleClasses = { "LabelSubText" },
        });
        root.AddChild(_designBox);

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
                ShowSliders(preset);
                Picked(preset);
            };
            presets.AddChild(button);
        }

        root.AddChild(presets);

        _charges = new Label();
        root.AddChild(_charges);

        ShowMode(CMUVehicleSprayPainterMode.Paint);
    }

    /// <summary>
    /// Shows the painter as it is without reporting anything as picked: its mode, its paint and its
    /// design in its colours.
    /// </summary>
    public void SetState(CMUVehicleSprayPainterMode mode, Color paint, ProtoId<CMURimDesignPrototype> design, List<Color> colors)
    {
        _paint = paint;
        if (_prototypes.TryIndex(design, out var rim))
        {
            _design = rim;
            _designColors.Clear();
            _designColors.AddRange(colors.Count == CMUVehicleSprayPainterSystem.DesignColorCount
                ? colors
                : new List<Color> { paint, rim.Accent, rim.LipColor, rim.CapColor });
            if (_designs.TryGetValue(rim.ID, out var entry))
                entry.Button.Pressed = true;

            RefreshDesign();
        }

        ShowMode(mode);
    }

    public void SetCharges(int current, int max)
    {
        _charges.Text = Loc.GetString("cmu-vehicle-spray-painter-charges", ("current", current), ("max", max));
    }

    private void PickMode(CMUVehicleSprayPainterMode mode)
    {
        if (_mode == mode)
            return;

        ShowMode(mode);
        OnModePicked?.Invoke(mode);
    }

    private void ShowMode(CMUVehicleSprayPainterMode mode)
    {
        _mode = mode;
        _paintTab.Pressed = mode == CMUVehicleSprayPainterMode.Paint;
        _designTab.Pressed = mode == CMUVehicleSprayPainterMode.Design;
        _paintBox.Visible = mode == CMUVehicleSprayPainterMode.Paint;
        _designBox.Visible = mode == CMUVehicleSprayPainterMode.Design;
        ShowSliders(Current());
    }

    private void PickDesign(CMURimDesignPrototype design)
    {
        var baseColor = _designColors.Count > 0 ? _designColors[0] : FactoryWheels;
        _design = design;
        _designColors.Clear();
        _designColors.AddRange(new[] { baseColor, design.Accent, design.LipColor, design.CapColor });
        if (_region != 0 && !design.Regions.Contains((CMURimRegion) _region))
            _region = 0;

        RefreshDesign();
        ShowSliders(Current());
        OnDesignPicked?.Invoke(design.ID, new List<Color>(_designColors));
    }

    private void PickRegion(int region)
    {
        _region = region;
        ShowSliders(Current());
    }

    /// <summary>
    /// The colour the sliders mix: the paint, or the design colour being picked.
    /// </summary>
    private Color Current()
    {
        return _mode == CMUVehicleSprayPainterMode.Design && _region < _designColors.Count ? _designColors[_region] : _paint;
    }

    private void Picked(Color color)
    {
        if (_mode == CMUVehicleSprayPainterMode.Paint || _design == null || _designColors.Count != Regions.Length)
        {
            _paint = color;
            OnColorPicked?.Invoke(color);
            return;
        }

        _designColors[_region] = color;
        RefreshDesign();
        OnDesignPicked?.Invoke(_design.ID, new List<Color>(_designColors));
    }

    /// <summary>
    /// The picked design's preview in its colours, and the colour chips it uses.
    /// </summary>
    private void RefreshDesign()
    {
        if (_design == null || _designColors.Count != Regions.Length)
            return;

        if (_designs.TryGetValue(_design.ID, out var entry))
            entry.Preview.Texture = Preview(_design, _designColors);

        for (var i = 0; i < Regions.Length; i++)
        {
            _regionSwatches[i].BackgroundColor = _designColors[i];
            _regionButtons[i].Visible = i == 0 || _design.Regions.Contains((CMURimRegion) i);
            _regionButtons[i].Pressed = i == _region;
        }
    }

    /// <summary>
    /// A design on a wheel in its colours; the design's last preview is freed.
    /// </summary>
    private Texture Preview(CMURimDesignPrototype design, IReadOnlyList<Color> colors)
    {
        using var image = CMUVehicleRimVisualSystem.PreviewImage(design, colors);
        var texture = _clyde.LoadTextureFromImage(image);
        if (_textures.Remove(design.ID, out var old))
            old.Dispose();

        _textures[design.ID] = texture;
        return texture;
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
        Picked(color);
    }

    private void HexEntered(string text)
    {
        if (!Color.TryFromHex(text.StartsWith('#') ? text : $"#{text}", out var color))
        {
            _hex.Text = SliderColor().ToHexNoAlpha();
            return;
        }

        color = color.WithAlpha(1f);
        ShowSliders(color);
        Picked(color);
    }

    private void ShowSliders(Color color)
    {
        _updating = true;
        _sliders[0].SetValueWithoutEvent(MathF.Round(color.R * 255));
        _sliders[1].SetValueWithoutEvent(MathF.Round(color.G * 255));
        _sliders[2].SetValueWithoutEvent(MathF.Round(color.B * 255));
        _updating = false;
        Show(color);
    }

    private void Show(Color color)
    {
        for (var i = 0; i < 3; i++)
        {
            _values[i].Text = ((int) _sliders[i].Value).ToString();
        }

        _hex.Text = color.ToHexNoAlpha();
        if (_mode == CMUVehicleSprayPainterMode.Paint)
            _swatch.BackgroundColor = color;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }
}
