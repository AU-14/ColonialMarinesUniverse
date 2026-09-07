using System;
using System.Linq;
using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.Decals;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.UserInterface.ColorPicker;

/// <summary>
///     The picking surface behind <see cref="CmuColorPicker"/>: a preset palette, a saturation/value
///     square, a hue strip, the hex field, and a preview line drawn on the chat background.
/// </summary>
/// <remarks>
///     Deliberately a plain control with no panel of its own. It expands inside the options tab, and
///     the tab is already inside the window frame and the tab container's panel - another bordered
///     box here would be a fourth concentric rectangle.
/// </remarks>
public sealed class CmuColorPickerPanel : Control
{
    private const int PaletteColumns = 6;
    private const int SwatchSize = 18;
    private const int FieldHeight = 130;

    private readonly IPrototypeManager _prototypes;
    private readonly IConfigurationManager _cfg;
    private readonly IClientPreferencesManager _preferences;

    private readonly GridContainer _palette;
    private readonly ColorFieldControl _saturationValue;
    private readonly ColorFieldControl _hue;
    private readonly LineEdit _hexEdit;
    private readonly LineEdit _previewEdit;

    /// <summary>
    ///     HSVa is the source of truth rather than the RGB colour. Round-tripping through RGB loses
    ///     the hue as soon as saturation or value hits zero, which makes the strip jump to red the
    ///     moment you drag into a corner of the square.
    /// </summary>
    private Vector4 _hsv = Vector4.UnitW;

    private bool _updating;

    public event Action<Color>? OnColorPicked;

    public Color Color
    {
        get => Robust.Shared.Maths.Color.FromHsv(_hsv);
        set
        {
            _hsv = Robust.Shared.Maths.Color.ToHsv(value);
            UpdateFields();
        }
    }

    public CmuColorPickerPanel()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _cfg = IoCManager.Resolve<IConfigurationManager>();
        _preferences = IoCManager.Resolve<IClientPreferencesManager>();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            Margin = new Thickness(52, 2, 0, 6),
        };
        AddChild(root);

        var fields = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        root.AddChild(fields);

        // X varies saturation, Y varies value - the arrangement every other colour picker uses.
        // Roughly square: a long thin gradient makes fine adjustment along the short axis awkward.
        _saturationValue = new ColorFieldControl(new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 0))
        {
            MinSize = new Vector2(150, FieldHeight),
        };
        _saturationValue.OnValueChanged += OnSaturationValueChanged;
        fields.AddChild(_saturationValue);

        // A strip: no horizontal axis, hue down the vertical one.
        _hue = new ColorFieldControl(Vector4.Zero, new Vector4(1, 0, 0, 0))
        {
            MinSize = new Vector2(22, FieldHeight),
        };
        _hue.OnValueChanged += OnHueChanged;
        fields.AddChild(_hue);

        // The space freed by squaring off the gradient: type a colour rather than hunt for it, and
        // see it against the surface it will actually be read on.
        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        fields.AddChild(side);

        // Presets sit beside the gradient rather than above it: stacked full-width they cost a whole
        // row of height for twelve small squares, and this column has room going spare.
        _palette = new GridContainer
        {
            Columns = PaletteColumns,
            HSeparationOverride = 3,
            VSeparationOverride = 3,
        };
        side.AddChild(_palette);

        var hexRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        side.AddChild(hexRow);
        hexRow.AddChild(new Label { Text = Loc.GetString("cmu-color-picker-hex"), VerticalAlignment = VAlignment.Center });

        _hexEdit = new LineEdit { HorizontalExpand = true };
        _hexEdit.OnTextEntered += OnHexEntered;
        _hexEdit.OnFocusExit += _ => UpdateHexText();
        hexRow.AddChild(_hexEdit);

        side.AddChild(new Label { Text = Loc.GetString("cmu-color-picker-preview") });

        // Chat's own background rather than the CRT panel: the whole point is judging the colour
        // against the surface the text will be read on.
        var previewPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = StyleNano.ChatBackgroundColor,
                BorderColor = StyleNano.CrtGreenDim,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };
        side.AddChild(previewPanel);

        // LineEdit resolves its font colour from the stylesheet alone - there is no per-instance
        // override - so the picked colour is applied with Modulate over a transparent box.
        _previewEdit = new LineEdit
        {
            HorizontalExpand = true,
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Robust.Shared.Maths.Color.Transparent },
            Text = GetDefaultPreviewText(),
        };
        previewPanel.AddChild(_previewEdit);
    }

    /// <summary>
    ///     Seeds the preview with something the player will recognise: one of their own chat
    ///     highlights, failing that their selected character's name, failing that a prompt.
    /// </summary>
    private string GetDefaultPreviewText()
    {
        var highlights = _cfg.GetCVar(CCVars.ChatHighlights);
        if (!string.IsNullOrWhiteSpace(highlights))
        {
            var first = highlights
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        // Preferences only exist once connected and loaded, so this is a best-effort lookup.
        if (_preferences.ServerDataLoaded &&
            _preferences.Preferences is { } prefs &&
            prefs.Characters.ContainsKey(prefs.SelectedCharacterIndex) &&
            !string.IsNullOrWhiteSpace(prefs.SelectedCharacter.Name))
        {
            return prefs.SelectedCharacter.Name;
        }

        return Loc.GetString("cmu-color-picker-preview-placeholder");
    }

    /// <summary>
    ///     Fills the preset row from a <c>palette</c> prototype - note the prototype kind behind
    ///     <see cref="ColorPalettePrototype"/> is "palette", not "colorPalette". An unknown or absent
    ///     id just leaves the row empty rather than failing: the square and strip still work.
    /// </summary>
    public void SetPalette(string? paletteId)
    {
        _palette.DisposeAllChildren();

        if (string.IsNullOrWhiteSpace(paletteId) ||
            !_prototypes.TryIndex<ColorPalettePrototype>(paletteId, out var prototype))
        {
            _palette.Visible = false;
            return;
        }

        _palette.Visible = true;

        foreach (var (name, color) in prototype.Colors)
        {
            var swatch = new ContainerButton
            {
                MinSize = new Vector2(SwatchSize, SwatchSize),
                ToolTip = name,
                // Baked rather than a style class: the box has to carry the swatch's own colour, and
                // a StyleBoxOverride beats the stylesheet anyway.
                StyleBoxOverride = new StyleBoxFlat
                {
                    BackgroundColor = color,
                    BorderColor = StyleNano.CrtGreenDim,
                    BorderThickness = new Thickness(1),
                },
            };

            var captured = color;
            swatch.OnPressed += _ =>
            {
                Color = captured;
                OnColorPicked?.Invoke(Color);
            };

            _palette.AddChild(swatch);
        }
    }

    private void OnHexEntered(LineEdit.LineEditEventArgs args)
    {
        if (_updating)
            return;

        // FromHex's fallback overload rather than a bare parse: this field is user-editable, and the
        // parsing one throws on anything malformed.
        var parsed = Robust.Shared.Maths.Color.FromHex(args.Text.Trim(), Color);
        Color = parsed;
        OnColorPicked?.Invoke(parsed);
    }

    private void OnSaturationValueChanged(Vector2 value)
    {
        if (_updating)
            return;

        _hsv.Y = value.X;
        _hsv.Z = value.Y;
        UpdateFields();
        OnColorPicked?.Invoke(Color);
    }

    private void OnHueChanged(Vector2 value)
    {
        if (_updating)
            return;

        _hsv.X = value.Y;
        UpdateFields();
        OnColorPicked?.Invoke(Color);
    }

    private void UpdateFields()
    {
        _updating = true;

        // The square is drawn around the current hue; the strip around a fully saturated, bright
        // version of it, so the ramp stays readable no matter how washed out the chosen colour is.
        _saturationValue.SetBaseColorHsv(_hsv);
        _saturationValue.SetValueWithoutEvent(new Vector2(_hsv.Y, _hsv.Z));

        _hue.SetBaseColorHsv(new Vector4(_hsv.X, 1f, 1f, 1f));
        _hue.SetValueWithoutEvent(new Vector2(0f, _hsv.X));

        _previewEdit.Modulate = Color;
        UpdateHexText();

        _updating = false;
    }

    private void UpdateHexText()
    {
        var wasUpdating = _updating;
        _updating = true;
        _hexEdit.Text = $"#{Color.ToHex()[1..]}";
        _updating = wasUpdating;
    }
}
