using System;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     The obsidian ladder the ghost roles window is built from, and the neutral one it falls back to
///     with the CRT theme off. Same contract as <see cref="CrtTerminalPalette"/>: every member is a
///     property and every one is mode-aware.
/// </summary>
public static class HivePalette
{
    private static bool Crt => StyleNano.CrtUiEnabled;

    /// <summary>Window body.</summary>
    public static Color Surface0 => Crt ? Color.FromHex("#0B0B0D") : CrtTerminalPalette.Surface0;

    /// <summary>A banner, the rail, the filter bar.</summary>
    public static Color Surface1 => Crt ? Color.FromHex("#121215") : CrtTerminalPalette.Surface1;

    /// <summary>Header, preview frame, selected rail entry, hover.</summary>
    public static Color Surface2 => Crt ? Color.FromHex("#1B1B20") : CrtTerminalPalette.Surface2;

    /// <summary>A raffle bar's unfilled track.</summary>
    public static Color Surface3 => Crt ? Color.FromHex("#26262E") : CrtTerminalPalette.Surface3;

    /// <summary>Structural hairline - window edge, rail edge, under a banner.</summary>
    public static Color Line => Crt ? Color.FromHex("#33333D") : CrtTerminalPalette.Line;

    /// <summary>The seam between two instance rows of the same role. Darker than <see cref="Line"/>.</summary>
    public static Color Rule => Crt ? Color.FromHex("#17171B") : CrtTerminalPalette.Surface1;

    /// <summary>Descriptions and secondary labels.</summary>
    public static Color TextDim => Crt ? Color.FromHex("#9C9CAB") : CrtTerminalPalette.TextDim;

    /// <summary>Body text - a location, a rail entry's name.</summary>
    public static Color Text => Crt ? Color.FromHex("#D3D3DC") : CrtTerminalPalette.Text;

    /// <summary>A role's name, the window title.</summary>
    public static Color TextBright => Crt ? Color.FromHex("#F2F2F7") : CrtTerminalPalette.TextBright;

    /// <summary>Counts on unselected rail entries, and the fallback "?" on a card.</summary>
    public static Color TextFaint => Crt ? Color.FromHex("#7A7A8C") : CrtTerminalPalette.TextDim.WithAlpha(0.7f);

    /// <summary>Window title, raffle clock, and the fallback accent for a category without one.</summary>
    public static Color Accent => Crt ? Color.FromHex("#C993FF") : CrtTerminalPalette.Accent;

    /// <summary>The edge of an outlined control. Deliberately not <see cref="Line"/>, which is 1.57:1 here.</summary>
    public static Color ControlEdge => Crt ? Color.FromHex("#636375") : CrtTerminalPalette.Line;

    /// <summary>The label on an outlined control - between <see cref="TextDim"/> and <see cref="Text"/>.</summary>
    public static Color ControlText => Crt ? Color.FromHex("#ADADBB") : CrtTerminalPalette.Text;

    private const float AccentMinContrast = 8.5f;

    /// <summary>
    ///     <paramref name="accent"/> made readable on this window's ground, giving up as little of
    ///     itself as that takes. Raise-only: a colour already clearing the floor is returned untouched.
    /// </summary>
    public static Color Readable(Color accent)
    {
        if (!Crt || Contrast(accent, Surface0) >= AccentMinContrast)
            return accent;

        var hsv = Color.ToHsv(accent);
        var low = 0f;
        var high = hsv.Y;

        // Saturation is what is spent, not value: at full saturation this hue cannot reach the floor
        // at any brightness, so searching value alone would never terminate usefully.
        for (var i = 0; i < 24; i++)
        {
            var mid = (low + high) / 2f;
            if (Contrast(AtSaturation(hsv.X, mid), Surface0) >= AccentMinContrast)
                low = mid;
            else
                high = mid;
        }

        return AtSaturation(hsv.X, low);
    }

    private static Color AtSaturation(float hue, float saturation) =>
        Color.FromHsv(new Vector4(hue, saturation, 1f, 1f));

    private static float Contrast(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
    }

    private static float RelativeLuminance(Color color) =>
        0.2126f * Linear(color.R) + 0.7152f * Linear(color.G) + 0.0722f * Linear(color.B);

    private static float Linear(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    // Rec. 709 luminance of the matching surface, on the sRGB values as stored - what TintedFill wants.
    private const float RowLuminance = 0.0714f;
    private const float RowSaturation = 0.55f;
    private const float ControlLuminance = 0.1073f;
    private const float ControlSaturation = 0.38f;

    /// <summary>A row fill carrying <paramref name="hue"/> at <see cref="Surface1"/>'s luminance.</summary>
    public static Color RowTint(Color hue) =>
        Crt ? CrtTerminalPalette.TintedFill(hue, RowSaturation, RowLuminance) : CrtTerminalPalette.Surface1;

    /// <summary>The face of the primary button, carrying <paramref name="accent"/> as a tint.</summary>
    public static Color ControlFace(Color accent) =>
        Crt ? CrtTerminalPalette.TintedFill(accent, ControlSaturation, ControlLuminance) : CrtTerminalPalette.Surface2;

    /// <summary>The face for this window's text at <paramref name="size"/> points.</summary>
    public static Font Font(IResourceCache cache, int size) =>
        Crt ? StyleNano.GetCrtFont(cache, size) : cache.NotoStack(size: size);
}
