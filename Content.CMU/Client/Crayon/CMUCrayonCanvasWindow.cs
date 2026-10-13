using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Crayon;
using Content.Shared.Crayon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Crayon;

public sealed class CMUCrayonCanvasWindow : DefaultWindow
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly CMUCrayonCanvas _canvas = new();
    private readonly LineEdit _name = new() { HorizontalExpand = true };
    private readonly Label _needs = new();
    private readonly Label _error = new() { FontColorOverride = Color.Red };
    private readonly ColorSelectorSliders _custom = new() { Visible = false };
    private readonly Dictionary<Color, string> _crayons = new();

    public event Action<string, CMUCrayonSymbol>? OnSaved;

    public CMUCrayonCanvasWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("cmu-crayon-canvas-title");
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.Abstract && proto.TryGetComponent<CrayonComponent>(out var crayon, _factory) && !crayon.SelectableColor)
                _crayons.TryAdd(crayon.Color, proto.Name);
        }

        var small = new Button { Text = "32x32", ToggleMode = true, Pressed = true };
        var large = new Button { Text = "64x64", ToggleMode = true };
        small.OnPressed += _ => SetSize(CMUCrayonSymbol.Small, small, large);
        large.OnPressed += _ => SetSize(CMUCrayonSymbol.Large, large, small);

        var palette = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        foreach (var (color, name) in _crayons)
        {
            palette.AddChild(Swatch(color, name));
        }

        var eraser = new Button { Text = Loc.GetString("cmu-crayon-canvas-eraser") };
        eraser.OnPressed += _ => _canvas.Brush = null;
        var custom = new Button { Text = Loc.GetString("cmu-crayon-canvas-custom") };
        custom.OnPressed += _ =>
        {
            _custom.Visible = !_custom.Visible;
            _canvas.Brush = _custom.Color;
        };
        _custom.OnColorChanged += c => _canvas.Brush = c;
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
                _custom,
                _canvas,
                Row(fill, clear),
                _needs,
                _error,
                Row(save),
            },
        });

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

    private void UpdateNeeds()
    {
        var names = _canvas.Colors().Select(c => _crayons.GetValueOrDefault(c) ?? Loc.GetString("cmu-crayon-canvas-rainbow"));
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

    private Button Swatch(Color color, string name)
    {
        var button = new Button
        {
            ToolTip = name,
            MinSize = new Vector2(24, 24),
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = color },
        };
        button.OnPressed += _ => _canvas.Brush = color;
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
