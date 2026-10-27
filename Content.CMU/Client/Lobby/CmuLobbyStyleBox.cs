using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Lobby;

/// <summary>Reads the live palette without rebuilding controls or changing their layout.</summary>
public sealed class CmuLobbyStyleBox(Color? accent, Color? fill, bool selected, Button? button = null) : StyleBox
{
    private readonly StyleBoxFlat _box = new();
    private Color? _lastAccent;
    private Color? _lastSurface;
    private Color? _lastTheme;
    private bool _lastHover;
    private bool _lastSelected;
    private bool _lastDisabled;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var theme = CmuLobbyPresentation.Accent;
        var color = accent ?? theme;
        var surface = CmuLobbyPresentation.Surface;
        var hover = button is { IsHovered: true, Disabled: false };
        var active = selected || button is { Pressed: true };
        var disabled = button is { Disabled: true };
        if (_lastAccent != color || _lastSurface != surface || _lastTheme != theme ||
            _lastHover != hover || _lastSelected != active || _lastDisabled != disabled)
        {
            _lastAccent = color;
            _lastSurface = surface;
            _lastTheme = theme;
            _lastHover = hover;
            _lastSelected = active;
            _lastDisabled = disabled;
            color = CmuLobbyPresentation.ThemedAccent(color);
            // Pin brightness rather than tint opacity: yellow needs less tint than blue to
            // occupy the same visual layer. Composite the fill, never fade its text or sprites.
            var luminance = active ? 0.095f : hover ? 0.065f : button != null && !disabled ? 0.038f : 0.022f;
            _box.BackgroundColor = CmuLobbyPresentation.TintedSurface(fill ?? surface, color, luminance);
            _box.BorderColor = CmuLobbyPresentation.ReadableColor(
                Color.InterpolateBetween(surface, color, active ? 1f : hover ? 0.85f : 0.55f),
                _box.BackgroundColor, button != null ? 3f : 1.8f);
            _box.BorderThickness = active ? new Thickness(3, 1, 1, 2) : new Thickness(1);
        }

        _box.Draw(handle, box, uiScale);
        if (button != null)
            handle.DrawLine(new Vector2(box.Left + uiScale, box.Top + uiScale),
                new Vector2(box.Right - uiScale, box.Top + uiScale), Color.White.WithAlpha(active ? 0.12f : 0.045f));
    }
}

public static partial class CmuLobbyPresentation
{
    private static Color? _paletteAccent;
    private static Color? _paletteSurface;
    private static Color _readableAccent;

    public static Color Accent
    {
        get
        {
            var accent = StyleNano.CrtGreen;
            var surface = Surface;
            if (_paletteAccent != accent || _paletteSurface != surface)
            {
                _paletteAccent = accent;
                _paletteSurface = surface;
                _readableAccent = ReadableColor(accent, surface);
            }
            return _readableAccent;
        }
    }

    public static Color Surface => CrtTerminalPalette.Surface1;

    /// <summary>Keep faction/status meaning while bringing it into the player's chosen palette.</summary>
    public static Color ThemedAccent(Color semantic) => ReadableColor(
        Color.InterpolateBetween(semantic.WithAlpha(1f), Accent, 0.22f), Surface);

    public static Color TintedSurface(Color surface, Color accent, float luminance)
    {
        // A little neutral light preserves depth even with black or very saturated UI colours.
        var baseColor = Color.InterpolateBetween(surface.WithAlpha(1f), Color.FromHex("#252733"), 0.45f);
        var tint = Color.InterpolateBetween(accent, Color.White, 0.12f);
        var low = 0f;
        var high = 1f;
        for (var i = 0; i < 12; i++)
        {
            var amount = (low + high) / 2;
            if (RelativeLuminance(Color.InterpolateBetween(baseColor, tint, amount)) >= luminance)
                high = amount;
            else
                low = amount;
        }
        return Color.InterpolateBetween(baseColor, tint, high);
    }

    /// <summary>Retains the hue where possible, lightening only enough for readable contrast.</summary>
    public static Color ReadableColor(Color color, Color background, float minimumRatio = 4.5f)
    {
        color = color.WithAlpha(1f);
        var backgroundLuminance = RelativeLuminance(background);
        bool Passes(Color candidate) =>
            (RelativeLuminance(candidate) + 0.05f) / (backgroundLuminance + 0.05f) >= minimumRatio;
        if (Passes(color))
            return color;

        var low = 0f;
        var high = 1f;
        for (var i = 0; i < 12; i++)
        {
            var amount = (low + high) / 2;
            if (Passes(Color.InterpolateBetween(color, Color.White, amount)))
                high = amount;
            else
                low = amount;
        }
        return Color.InterpolateBetween(color, Color.White, high);
    }

    private static float RelativeLuminance(Color color)
    {
        static float Linear(float channel) => channel <= 0.04045f
            ? channel / 12.92f
            : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * Linear(color.R) + 0.7152f * Linear(color.G) + 0.0722f * Linear(color.B);
    }
}
