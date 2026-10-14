using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Crayon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Crayon;

/// <summary>
/// Draws a custom crayon symbol with the default box's crayons, each in its tones from light to dark,
/// or with any colour for a rainbow crayon.
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
    private readonly ColorSelectorSliders _custom = new() { Visible = false };

    public event Action<string, CMUCrayonSymbol>? OnSaved;

    public CMUCrayonCanvasWindow(Color brush)
    {
        IoCManager.InjectDependencies(this);
        _palette = _entities.System<CMUCrayonPaletteSystem>();
        Title = Loc.GetString("cmu-crayon-canvas-title");

        var small = new Button { Text = "32x32", ToggleMode = true, Pressed = true };
        var large = new Button { Text = "64x64", ToggleMode = true };
        small.OnPressed += _ => SetSize(CMUCrayonSymbol.Small, small, large);
        large.OnPressed += _ => SetSize(CMUCrayonSymbol.Large, large, small);

        var palette = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var crayons = _palette.Crayons;
        for (var i = 0; i < crayons.Count; i++)
        {
            var family = i;
            palette.AddChild(Swatch(crayons[i].Color, crayons[i].Name, () => PickCrayon(family)));
        }

        var eraser = new Button { Text = Loc.GetString("cmu-crayon-canvas-eraser") };
        eraser.OnPressed += _ => SetBrush(null);
        var custom = new Button { Text = Loc.GetString("cmu-crayon-canvas-custom") };
        custom.OnPressed += _ =>
        {
            _custom.Visible = !_custom.Visible;
            SetBrush(_custom.Color);
        };
        _custom.OnColorChanged += c => SetBrush(c);
        palette.AddChild(eraser);
        palette.AddChild(custom);

        var fill = new Button { Text = Loc.GetString("cmu-crayon-canvas-fill"), ToggleMode = true };
        fill.OnToggled += args => _canvas.Fill = args.Pressed;
        var clear = new Button { Text = Loc.GetString("cmu-crayon-canvas-clear") };
        clear.OnPressed += _ => _canvas.Clear();
        var save = new Button { Text = Loc.GetString("cmu-crayon-canvas-save") };
        save.OnPressed += _ => Save();

        _canvas.OnChanged += UpdateNeeds;
        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Children =
            {
                Row(new Label { Text = Loc.GetString("cmu-crayon-canvas-name") }, _name, small, large),
                palette,
                Row(new Label { Text = Loc.GetString("cmu-crayon-canvas-tone") }, _tones),
                _brush,
                _custom,
                _canvas,
                Row(fill, clear),
                _needs,
                _error,
                Row(save),
            },
        });

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

        UpdateNeeds();
    }

    public void Load(string name, CMUCrayonSymbol symbol)
    {
        _name.Text = name;
        _canvas.Load(symbol);
    }

    private void SetSize(int size, Button pressed, Button other)
    {
        pressed.Pressed = true;
        other.Pressed = false;
        _canvas.Resize(size);
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
            Close();
        }
    }

    private static Button Swatch(Color color, string name, Action pressed)
    {
        var button = new Button { ToolTip = name };
        button.AddChild(new PanelContainer
        {
            MinSize = new Vector2(20, 20),
            PanelOverride = new StyleBoxFlat { BackgroundColor = color, BorderColor = Color.Black, BorderThickness = new Thickness(1) },
        });
        button.OnPressed += _ => pressed();
        return button;
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
