using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Crayon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Crayon;

/// <summary>
/// Draws a custom crayon symbol with the default box's crayons, each in its tones from light to dark:
/// a brush of a few sizes, fill, lines, circles and gradients between two colours, mirrored across
/// either axis or both. It can pop out into a window of its own, to draw on another screen.
/// </summary>
public sealed class CMUCrayonCanvasWindow : DefaultWindow
{
    [Dependency] private IEntityManager _entities = default!;

    private readonly CMUCrayonPaletteSystem _palette;
    private readonly CMUCrayonCanvas _canvas = new();
    private readonly LineEdit _name = new() { HorizontalExpand = true };
    private readonly BoxContainer _tones = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
    private readonly Label _brush = new();
    private readonly Label _needs = new();
    private readonly Label _error = new() { FontColorOverride = Color.Red };
    private readonly Button _small = new() { Text = "32x32", ToggleMode = true, Pressed = true };
    private readonly Button _large = new() { Text = "64x64", ToggleMode = true };
    private readonly Button _popOut = new() { Text = Loc.GetString("cmu-crayon-canvas-pop-out"), StyleClasses = { "OpenRight" }, VerticalAlignment = VAlignment.Center };
    private bool _saved;
    private readonly BoxContainer _body;
    private OSWindow? _outside;
    private readonly PanelContainer _gradientA = SwatchPanel(Color.White);
    private readonly PanelContainer _gradientB = SwatchPanel(Color.Black);
    private Color _from = Color.White;
    private Color _to = Color.Black;

    // A gradient between two box crayons steps through their tones; one with a rainbow crayon's colour blends in this many.
    private const int RainbowGradientSteps = 8;

    public event Action<string, CMUCrayonSymbol>? OnSaved;

    public CMUCrayonCanvasWindow(Color brush)
    {
        IoCManager.InjectDependencies(this);
        _palette = _entities.System<CMUCrayonPaletteSystem>();
        Title = Loc.GetString("cmu-crayon-canvas-title");

        _small.OnPressed += _ => ResizeCanvas(CMUCrayonSymbol.Small);
        _large.OnPressed += _ => ResizeCanvas(CMUCrayonSymbol.Large);

        var palette = new GridContainer { Columns = 8, HSeparationOverride = 2, VSeparationOverride = 2 };
        var crayons = _palette.Crayons;
        for (var i = 0; i < crayons.Count; i++)
        {
            var family = i;
            palette.AddChild(Swatch(crayons[i].Color, crayons[i].Name, () => PickCrayon(family)));
        }

        var eraser = new Button { Text = Loc.GetString("cmu-crayon-canvas-eraser"), HorizontalExpand = true };
        eraser.OnPressed += _ => SetBrush(null);

        var tools = Group(5,
            (Loc.GetString("cmu-crayon-canvas-brush-tool"), () => _canvas.Tool = CMUCrayonTool.Brush),
            (Loc.GetString("cmu-crayon-canvas-fill"), () => _canvas.Tool = CMUCrayonTool.Fill),
            (Loc.GetString("cmu-crayon-canvas-line"), () => _canvas.Tool = CMUCrayonTool.Line),
            (Loc.GetString("cmu-crayon-canvas-circle"), () => _canvas.Tool = CMUCrayonTool.Circle),
            (Loc.GetString("cmu-crayon-canvas-gradient"), () => _canvas.Tool = CMUCrayonTool.Gradient));

        var sizes = new List<(string, Action)>();
        for (var size = 1; size <= CMUCrayonCanvas.MaxBrushSize; size++)
        {
            var across = size;
            sizes.Add((across.ToString(), () => _canvas.BrushSize = across));
        }

        var mirrors = Group(4,
            (Loc.GetString("cmu-crayon-canvas-mirror-none"), () => _canvas.Mirror = CMUCrayonMirror.None),
            ("X", () => _canvas.Mirror = CMUCrayonMirror.X),
            ("Y", () => _canvas.Mirror = CMUCrayonMirror.Y),
            (Loc.GetString("cmu-crayon-canvas-mirror-both"), () => _canvas.Mirror = CMUCrayonMirror.Both));

        var gradientA = new Button { ToolTip = Loc.GetString("cmu-crayon-canvas-gradient-set") };
        gradientA.AddChild(_gradientA);
        gradientA.OnPressed += _ => SetGradient(true);
        var gradientB = new Button { ToolTip = Loc.GetString("cmu-crayon-canvas-gradient-set") };
        gradientB.AddChild(_gradientB);
        gradientB.OnPressed += _ => SetGradient(false);

        var clear = new Button { Text = Loc.GetString("cmu-crayon-canvas-clear") };
        clear.OnPressed += _ => _canvas.Clear();
        var save = new Button { Text = Loc.GetString("cmu-crayon-canvas-save"), MinWidth = 80 };
        save.OnPressed += _ => Save();
        _popOut.OnPressed += _ => PopOut();
        // Beside the window's close button.
        CloseButton.Parent?.AddChild(_popOut);
        _popOut.SetPositionInParent(CloseButton.GetPositionInParent());

        _canvas.OnChanged += UpdateNeeds;
        // Tools along the top, the canvas in the middle, mirror, clear and the name and save along the bottom.
        _canvas.HorizontalAlignment = HAlignment.Center;
        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(4),
            Children =
            {
                Row(Section("cmu-crayon-canvas-section-colours", Row(palette, eraser), _tones, _brush)),
                Row(
                    Section("cmu-crayon-canvas-section-tool", tools),
                    Section("cmu-crayon-canvas-size", Group(CMUCrayonCanvas.MaxBrushSize, sizes.ToArray())),
                    Section("cmu-crayon-canvas-gradient-colors", Row(gradientA, new Label { Text = ">" }, gradientB)),
                    Section("cmu-crayon-canvas-section-canvas", Row(_small, _large))),
                new PanelContainer { MinHeight = 2, PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#3A3A3A") } },
                _canvas,
                _needs,
                _error,
                new PanelContainer { MinHeight = 2, PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#3A3A3A") } },
                Row(new Label { Text = Loc.GetString("cmu-crayon-canvas-mirror") }, mirrors, clear),
                Row(new Label { Text = Loc.GetString("cmu-crayon-canvas-name") }, _name, save),
            },
        };
        Contents.AddChild(_body);

        // Start with the held crayon: its own colour if it is a box crayon's tone, else the box crayon nearest it.
        if (_palette.TryGetFamily(brush, out var held, out _))
        {
            ShowTones(held);
            SetBrush(brush);
        }
        else if (_palette.NearestFamily(brush) is var nearest and >= 0)
        {
            PickCrayon(nearest);
        }
        else
        {
            SetBrush(brush);
        }

        UpdateGradient();
        UpdateNeeds();
    }

    public void Load(string name, CMUCrayonSymbol symbol)
    {
        _name.Text = name;
        _canvas.Load(symbol);
        _small.Pressed = symbol.Size == CMUCrayonSymbol.Small;
        _large.Pressed = !_small.Pressed;
    }

    private void ResizeCanvas(int size)
    {
        _small.Pressed = size == CMUCrayonSymbol.Small;
        _large.Pressed = !_small.Pressed;
        _canvas.Resize(size);
    }

    /// <summary>
    /// Moves the editor into a window of its own, which can go on another screen. Closing that window
    /// brings it back into the game.
    /// </summary>
    private void PopOut()
    {
        if (_outside != null)
            return;

        _body.Orphan();
        _outside = new OSWindow { Title = Title ?? string.Empty, SetSize = Size };
        _outside.Show();
        _outside.Root?.AddChild(_body);
        _outside.Closed += PopIn;
        Close();
    }

    private void PopIn()
    {
        if (_outside == null)
            return;

        _outside = null;
        if (_saved)
            return;

        _body.Orphan();
        Contents.AddChild(_body);
        OpenCentered();
    }

    private void PickCrayon(int family)
    {
        ShowTones(family);
        SetBrush(_palette.Crayons[family].Color);
    }

    private void ShowTones(int family)
    {
        _tones.RemoveAllChildren();
        foreach (var (_, color) in CMUCrayonPaletteSystem.Shades(_palette.Crayons[family].Color))
        {
            _tones.AddChild(Swatch(color, Describe(color), () => SetBrush(color)));
        }
    }

    private void SetBrush(Color? color)
    {
        _canvas.Brush = color;
        _brush.Text = Loc.GetString("cmu-crayon-canvas-brush", ("color", color == null
            ? Loc.GetString("cmu-crayon-canvas-eraser")
            : Describe(color.Value)));
    }

    /// <summary>
    /// A colour as the crayon and tone that draws it, or its hex for a rainbow crayon's.
    /// </summary>
    private string Describe(Color color)
    {
        if (!_palette.TryGetFamily(color, out var family, out var tone))
            return color.ToHex();

        var name = _palette.Crayons[family].Name;
        return tone switch
        {
            > 0 => Loc.GetString("cmu-crayon-canvas-tone-lighter", ("crayon", name), ("steps", tone)),
            < 0 => Loc.GetString("cmu-crayon-canvas-tone-darker", ("crayon", name), ("steps", -tone)),
            _ => name,
        };
    }

    /// <summary>
    /// Makes the brush's colour the gradient's start or its end.
    /// </summary>
    private void SetGradient(bool start)
    {
        if (_canvas.Brush is not { } color)
            return;

        if (start)
            _from = color;
        else
            _to = color;

        UpdateGradient();
    }

    private void UpdateGradient()
    {
        _gradientA.PanelOverride = SwatchStyle(_from);
        _gradientB.PanelOverride = SwatchStyle(_to);
        _canvas.Gradient = GradientSteps(_from, _to);
    }

    /// <summary>
    /// The colours a gradient steps through: the tones between two of one box crayon, the nearest tones
    /// of either box crayon along a blend between two, or an even blend with a rainbow crayon's colour.
    /// </summary>
    private List<Color> GradientSteps(Color from, Color to)
    {
        var steps = new List<Color>();
        if (_palette.TryGetFamily(from, out var fromFamily, out var fromTone) &&
            _palette.TryGetFamily(to, out var toFamily, out var toTone))
        {
            if (fromFamily == toFamily)
            {
                var shades = CMUCrayonPaletteSystem.Shades(_palette.Crayons[fromFamily].Color);
                var step = fromTone < toTone ? 1 : -1;
                for (var tone = fromTone; ; tone += step)
                {
                    if (shades.FindIndex(s => s.Tone == tone) is var index and >= 0)
                        steps.Add(shades[index].Color);

                    if (tone == toTone)
                        break;
                }

                return steps;
            }

            var tones = CMUCrayonPaletteSystem.Shades(_palette.Crayons[fromFamily].Color);
            tones.AddRange(CMUCrayonPaletteSystem.Shades(_palette.Crayons[toFamily].Color));
            var count = 2 * CMUCrayonPaletteSystem.Tones + 1;
            for (var i = 0; i < count; i++)
            {
                var blend = Color.InterpolateBetween(from, to, i / (float) (count - 1));
                var nearest = tones.MinBy(s => Distance(s.Color, blend)).Color;
                if (steps.Count == 0 || steps[steps.Count - 1] != nearest)
                    steps.Add(nearest);
            }

            return steps;
        }

        for (var i = 0; i < RainbowGradientSteps; i++)
        {
            steps.Add(Color.InterpolateBetween(from, to, i / (float) (RainbowGradientSteps - 1)));
        }

        return steps;
    }

    private static float Distance(Color a, Color b)
    {
        return (a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B);
    }

    private void UpdateNeeds()
    {
        var names = _canvas.Colors().Select(c => _palette.TryGetFamily(c, out var family, out _)
            ? _palette.Crayons[family].Name
            : Loc.GetString("cmu-crayon-canvas-rainbow"));
        _needs.Text = Loc.GetString("cmu-crayon-canvas-needs", ("crayons", string.Join(", ", names.Distinct())));
    }

    private void Save()
    {
        var symbol = _canvas.ToSymbol();
        var name = _name.Text.Trim();
        if (name.Length == 0)
            _error.Text = Loc.GetString("cmu-crayon-canvas-error-name");
        else if (symbol.Palette.Count > CMUCrayonSymbol.MaxColors)
            _error.Text = Loc.GetString("cmu-crayon-canvas-error-colors", ("max", CMUCrayonSymbol.MaxColors));
        else if (!symbol.IsValid())
            _error.Text = Loc.GetString("cmu-crayon-canvas-error-empty");
        else
        {
            OnSaved?.Invoke(name, symbol);
            _saved = true;
            _outside?.Close();
            Close();
        }
    }

    private static Button Swatch(Color color, string name, Action pressed)
    {
        var button = new Button { ToolTip = name };
        button.AddChild(SwatchPanel(color));
        button.OnPressed += _ => pressed();
        return button;
    }

    private static PanelContainer SwatchPanel(Color color)
    {
        return new PanelContainer { MinSize = new Vector2(20, 20), PanelOverride = SwatchStyle(color) };
    }

    private static StyleBoxFlat SwatchStyle(Color color)
    {
        return new StyleBoxFlat { BackgroundColor = color, BorderColor = Color.Black, BorderThickness = new Thickness(1) };
    }

    /// <summary>
    /// Buttons in rows of a few, of which one is picked at a time, the first to start with.
    /// </summary>
    private static GridContainer Group(int columns, params (string Text, Action Picked)[] options)
    {
        var grid = new GridContainer { Columns = columns, HSeparationOverride = 2, VSeparationOverride = 2 };
        var group = new ButtonGroup();
        for (var i = 0; i < options.Length; i++)
        {
            var (text, picked) = options[i];
            var button = new Button { Text = text, ToggleMode = true, Group = group, Pressed = i == 0, HorizontalExpand = true };
            button.OnPressed += _ => picked();
            grid.AddChild(button);
        }

        return grid;
    }

    private static BoxContainer Section(string title, params Control[] controls)
    {
        var section = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        section.AddChild(new Label { Text = Loc.GetString(title), StyleClasses = { "LabelHeading" } });
        foreach (var control in controls)
        {
            section.AddChild(control);
        }

        return section;
    }

    private static BoxContainer Row(params Control[] controls)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        foreach (var control in controls)
        {
            row.AddChild(control);
        }

        return row;
    }
}
