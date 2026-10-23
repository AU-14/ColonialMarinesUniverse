using System;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     The surface ladder and text tones the CRT theme is built from - and the neutral ladder that
///     stands in for it when the theme is switched off.
/// </summary>
public static class CrtTerminalPalette
{
    // Ladder note (2026-08-19): Surface2 and Surface3 were widened from #142519 and #1C3323. The
    // old values put every surface inside the bottom ~20% of the range - greens of 16/26/37/51 out
    // of 255 - which is why nothing could be distinguished by fill and every boundary needed a
    // border, the root of the box-in-box problem. The step *ratios* are unchanged; only the
    // absolute level moved. Verified side by side in docs/cmu/crt-gallery.html before applying.
    //
    // These sit under the shader, and the scanline pass darkens everything on top of them, so they
    // read dimmer in game than in a browser. If they land too bright, the tested next step down is
    // Surface1 #0C1810 / Surface2 #132A1C / Surface3 #1C3F2D / Surface4 #285538 - do not go below
    // that, it collapses the banding back into one tone.

    private static bool Crt => StyleNano.CrtUiEnabled;

    private static Color Tint(string green)
    {
        var original = Color.ToHsv(Color.FromHex(green));
        var defaultAccent = Color.ToHsv(Color.FromHex("#46FF8E"));
        var accent = Color.ToHsv(StyleNano.CrtGreen);
        var hue = (original.X + accent.X - defaultAccent.X + 1f) % 1f;
        var saturation = Math.Clamp(original.Y * accent.Y / defaultAccent.Y, 0f, 1f);
        return Color.FromHsv(new Vector4(hue, saturation, original.Z, 1f));
    }

    /// <summary>Behind everything. Not pure black - a phosphor tube never is.</summary>
    public static Color Void => Crt ? Tint("#040705") : Color.FromHex("#0E0E10");

    /// <summary>Window body.</summary>
    public static Color Surface0 => Crt ? Tint("#071009") : Color.FromHex("#1A1A1D");

    /// <summary>A section or group within the body.</summary>
    public static Color Surface1 => Crt ? Tint("#0D1A12") : Color.FromHex("#212126");

    /// <summary>
    ///     One row inside a section, and the resting fill of a button.
    /// </summary>
    /// <remarks>
    ///     Off-theme this has to stay clear of <c>DefaultCrtPanelBackground</c> (#25252A), which is
    ///     what the panels under these rows are painted with. The first pass set it to exactly that
    ///     value and every button on the lobby vanished into the panel behind it - same fill, no
    ///     border, nothing to see. Distinct fills are half the fix; the border below is the rest.
    /// </remarks>
    public static Color Surface2 => Crt ? Tint("#152F20") : Color.FromHex("#343440");

    /// <summary>Header and status strips; hover.</summary>
    public static Color Surface3 => Crt ? Tint("#204833") : Color.FromHex("#42424F");

    /// <summary>
    ///     Selected. The ladder needed a fourth step: with only three, hover and selected both landed
    ///     on Surface3 and were indistinguishable, so a selected tab looked exactly like a hovered
    ///     one. Off-theme this is NanoUI's own button colour, which is what a pressed or selected
    ///     control looked like before any of this existed.
    /// </summary>
    public static Color Surface4 => Crt ? Tint("#2E6241") : Color.FromHex("#525266");

    /// <summary>Hairline, for the few places a rule still says something a fill cannot.</summary>
    public static Color Line => Crt ? Tint("#2A5238") : Color.FromHex("#4A4A57");

    /// <summary>Field labels and other secondary text.</summary>
    public static Color TextDim => Crt ? Tint("#4E9C6B") : Color.FromHex("#9A9A9A");

    /// <summary>Body text.</summary>
    public static Color Text => Crt ? Tint("#8FE9AE") : Color.FromHex("#E0E0E0");

    /// <summary>Headings and values worth reading first.</summary>
    public static Color TextBright => Crt ? Tint("#C9FFDC") : Color.White;

    /// <summary>
    ///     The phosphor itself. Bars, pips, active states. Off-theme it is NanoGold, matching
    ///     <see cref="StyleNano.CrtGreen"/>, which has always fallen back to the same colour - so
    ///     the two ways of asking for "the accent" agree in both modes.
    /// </summary>
    public static Color Accent => StyleNano.CrtGreen;

    public static Color Caution => Crt ? Color.FromHex("#FFB454") : StyleNano.ConcerningOrangeFore;

    public static Color Alert => Crt ? Color.FromHex("#FF4E5E") : StyleNano.DangerousRedFore;

    /// <summary>
    ///     Saturation of a chat row tint at full strength.
    /// </summary>
    public const float ChatTintSaturationFull = 0.16f;

    /// <summary>Saturation of a muted chat row tint. Same rung, same hues, less of them.</summary>
    public const float ChatTintSaturationMuted = 0.10f;

    /// <summary>
    ///     Saturation of a coloured control fill - a button face, not a chat row.
    /// </summary>
    public const float ControlTintSaturation = 0.30f;

    /// <summary>
    ///     A chat row fill carrying <paramref name="hue"/> at the luminance of <see cref="Surface1"/>.
    /// </summary>
    public static Color ChatRowTint(Color hue, float saturation) =>
        TintedFill(hue, saturation, Luminance(Surface1));

    /// <summary>
    ///     A coloured control fill carrying <paramref name="hue"/>, on the rung buttons already sit
    ///     on.
    /// </summary>
    public static Color ControlTint(Color hue) =>
        TintedFill(hue, ControlTintSaturation, Luminance(Surface2));

    /// <summary>
    ///     <paramref name="hue"/> at <paramref name="saturation"/>, scaled to sit at
    ///     <paramref name="luminance"/>. Pinning luminance rather than HSV value is the whole point:
    ///     at equal value a blue fill sinks into the ground while a green one floats.
    /// </summary>
    public static Color TintedFill(Color hue, float saturation, float luminance)
    {
        // HSV -> RGB is linear in value, so luminance is too. Build the hue at value 1 and scale
        // once rather than searching for the value that lands on the rung.
        var h = Color.ToHsv(hue).X;
        var full = Color.FromHsv(new Vector4(h, saturation, 1f, 1f));
        return Color.FromHsv(new Vector4(h, saturation, Math.Clamp(luminance / Luminance(full), 0f, 1f), 1f));
    }

    /// <summary>
    ///     Brightness ceiling for a channel tone. Below <see cref="Text"/>'s own (~0.82) because blue and
    ///     violet cannot reach that - a strict pin would clamp them at full value and hand back the neon
    ///     primaries this palette exists to avoid.
    /// </summary>
    public const float ChannelToneLuminance = 0.64f;

    /// <summary>
    ///     Contrast a channel tone must clear against the row it is drawn on. Above the usual 4.5
    ///     for body text, because the scanline pass darkens everything on top of these and a ratio
    ///     measured here is the best case.
    /// </summary>
    public const float ChannelToneMinContrast = 5.5f;

    /// <summary>
    ///     <paramref name="hue"/> made readable on the chat row it will be drawn on, giving up as
    ///     little of itself as that takes.
    /// </summary>
    public static Color ChannelTone(Color hue)
    {
        if (!Crt)
            return hue;

        var hsv = Color.ToHsv(hue);

        // The full-strength tint, whichever the player has set: muted is the same hue at lower saturation.
        var ground = ChatRowTint(hue, ChatTintSaturationFull);

        if (Contrast(AtSaturation(hsv.X, hsv.Y), ground) >= ChannelToneMinContrast)
            return AtSaturation(hsv.X, hsv.Y);

        var low = 0f;
        var high = hsv.Y;
        for (var i = 0; i < 24; i++)
        {
            var mid = (low + high) / 2f;
            if (Contrast(AtSaturation(hsv.X, mid), ground) >= ChannelToneMinContrast)
                low = mid;
            else
                high = mid;
        }

        return AtSaturation(hsv.X, low);
    }

    /// <summary>
    ///     <paramref name="hue"/> at <paramref name="saturation"/>, as bright as that pair allows up
    ///     to <see cref="ChannelToneLuminance"/>.
    /// </summary>
    private static Color AtSaturation(float hue, float saturation)
    {
        var full = Color.FromHsv(new Vector4(hue, saturation, 1f, 1f));
        var value = MathF.Min(1f, ChannelToneLuminance / Luminance(full));
        return Color.FromHsv(new Vector4(hue, saturation, value, 1f));
    }

    private static float Contrast(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
    }

    private static float RelativeLuminance(Color color)
    {
        return 0.2126f * Linear(color.R) + 0.7152f * Linear(color.G) + 0.0722f * Linear(color.B);
    }

    private static float Linear(float channel)
    {
        return channel <= 0.04045f
            ? channel / 12.92f
            : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>
    ///     Rec. 709 weights applied to the sRGB values as stored, not to linear light. Deliberate:
    ///     every other colour in this file is an sRGB hex compared against its neighbours the same
    ///     way, and converting here would put the tints on a different scale to the ladder they are
    ///     meant to sit on.
    /// </summary>
    private static float Luminance(Color color)
    {
        return 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
    }
}
