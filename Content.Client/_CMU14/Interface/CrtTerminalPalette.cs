using System;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     The surface ladder and text tones the CRT theme is built from - and the neutral ladder that
///     stands in for it when the theme is switched off.
/// </summary>
/// <remarks>
///     <para>
///     The shipped palette derived all eighteen of its colours from one hue and packed every surface
///     between <c>#000906</c> and <c>#032314</c> - about five percent of the luminance range, all of
///     it near-black. Three problems followed from that one fact, and all three showed up in
///     practice: surfaces could not be told apart by fill, so every boundary had to be a border,
///     which is what produces the box-in-box look; nothing could signal danger, because a fill could
///     only ever be the theme colour brighter; and a scanline had no luminance to modulate, so it was
///     either invisible or a colour cast.
///     </para>
///     <para>
///     This ladder fixes the cause rather than the symptoms. Four surface steps, each visibly lighter
///     than the last, so a panel can sit inside a panel and be read as separate without a rule
///     between them. Text is genuinely bright - an Aliens terminal is high contrast, dim green on
///     black is a different look entirely. Caution and alert are off-hue on purpose.
///     </para>
///     <para>
///     <b>Every member is a property, not a field, and every one of them is mode-aware.</b> That is
///     the whole reason this file reads the way it does. These used to be plain constants, so a
///     control wearing a <c>Crt*</c> style class kept its green fill after the theme was switched
///     off - and since the XAML names those classes directly, that was most of the lobby. Turning
///     the theme off left a green UI in a proportional font, which is nobody's design.
///     <see cref="StyleNano"/>'s own colours had always fallen back; this file was the one that did
///     not.
///     </para>
///     <para>
///     The off values are the stock NanoUI greys, keyed to what each step is used *for* rather than
///     to its green's luminance: the panel steps land on the existing neutral panel colours, and the
///     interactive steps land on NanoUI's own button colours, so a button in base mode is the slate
///     it always was.
///     </para>
/// </remarks>
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
    ///     Saturation of a chat row tint at full strength: <see cref="Surface2"/>'s own, so a green
    ///     channel lands exactly on that rung and every other hue is that same rung rotated.
    /// </summary>
    public const float ChatTintSaturationFull = 0.553f;

    /// <summary>Saturation of a muted chat row tint. Same rung, same hues, less of them.</summary>
    public const float ChatTintSaturationMuted = 0.30f;

    /// <summary>
    ///     A chat row fill carrying <paramref name="hue"/> at the luminance of <see cref="Surface2"/>,
    ///     the rung announcements already sit on. Pinning luminance rather than HSV value is the
    ///     whole point: at equal value a blue row sinks into the ground while a green one floats.
    /// </summary>
    public static Color ChatRowTint(Color hue, float saturation)
    {
        // HSV -> RGB is linear in value, so luminance is too. Build the hue at value 1 and scale
        // once rather than searching for the value that lands on the rung.
        var h = Color.ToHsv(hue).X;
        var full = Color.FromHsv(new Vector4(h, saturation, 1f, 1f));
        var value = Luminance(Surface2) / Luminance(full);
        return Color.FromHsv(new Vector4(h, saturation, Math.Clamp(value, 0f, 1f), 1f));
    }

    /// <summary>
    ///     Luminance every channel tone is pinned to. Below <see cref="Text"/>'s own (~0.82) because
    ///     blue and violet cannot reach that - a strict pin would clamp them at full value and hand
    ///     back the neon primaries this palette exists to avoid. Every hue is reachable here.
    /// </summary>
    public const float ChannelToneLuminance = 0.66f;

    /// <summary>
    ///     Saturation of a channel tone. Enough to tell nine channels apart at a glance, low enough
    ///     that they still read as tinted phosphor rather than as arbitrary UI colours.
    /// </summary>
    public const float ChannelToneSaturation = 0.42f;

    /// <summary>
    ///     <paramref name="hue"/> rebuilt at the channel band's fixed luminance and saturation.
    /// </summary>
    /// <remarks>
    ///     The same trick as <see cref="ChatRowTint"/>, aimed at text rather than fills: take the hue
    ///     only, and rebuild it at a known luminance. Picking channel colours by eye is what produced
    ///     the shipped set - <c>LightSkyBlue</c>, <c>HotPink</c>, <c>MediumPurple</c> - where the pink
    ///     burns and the purple is nearly unreadable on a dark ground, because equal HSV *value* is
    ///     nothing like equal brightness across hues.
    /// </remarks>
    public static Color ChannelTone(Color hue)
    {
        if (!Crt)
            return hue;

        var h = Color.ToHsv(hue).X;
        var full = Color.FromHsv(new Vector4(h, ChannelToneSaturation, 1f, 1f));
        var value = ChannelToneLuminance / Luminance(full);
        return Color.FromHsv(new Vector4(h, ChannelToneSaturation, Math.Clamp(value, 0f, 1f), 1f));
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
