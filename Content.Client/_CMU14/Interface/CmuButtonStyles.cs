using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     The semantic button set: one padding and one height across all of them, differing only in
///     fill and text tone.
/// </summary>
/// <remarks>
///     <para>
///     Written because the Observe window needed a coloured button and there was nowhere to get one.
///     The stylesheet offers <c>CrtButton</c> and <c>CrtAttentionButton</c> - two steps of the same
///     green - so anything that had to say "this one is dangerous" hand-rolled a
///     <see cref="Button.StyleBoxOverride"/> at the call site. That is how the Observe window ended up
///     with its own copy, and it is how the next window would have too.
///     </para>
///     <para>
///     It also fixes a real bug rather than only adding colour. <c>CrtAttentionButton</c> and a plain
///     unstyled <see cref="Button"/> carry <em>different content margins</em>, so a row mixing the two
///     had one label sitting closer to its edge than its neighbours - which read as a clipped button.
///     Every variant here uses the same margins, so a mixed row lines up.
///     </para>
///     <para>
///     Fills come from <see cref="CrtTerminalPalette.ChatRowTint"/>, which pins a hue to
///     <see cref="CrtTerminalPalette.Surface2"/>'s luminance rather than its HSV value. At equal value
///     a red fill reads far heavier than a green one against a dark ground; pinning luminance is what
///     lets a row of differently-coloured buttons weigh the same.
///     </para>
/// </remarks>
public static class CmuButtonStyles
{
    public enum Variant
    {
        /// <summary>Default. Most buttons.</summary>
        Neutral,

        /// <summary>The one action the window exists to offer.</summary>
        Affirm,

        /// <summary>Consequential but reversible - observing, leaving, resetting.</summary>
        Caution,

        /// <summary>Irreversible, or elevated powers.</summary>
        Danger,
    }

    /// <summary>Shared by every variant, so a mixed row aligns.</summary>
    private const int PadHorizontal = 12;
    private const int PadTop = 6;
    private const int PadBottom = 4;

    public static void Apply(Button button, Variant variant)
    {
        var hue = HueOf(variant);

        button.StyleBoxOverride = MakeBox(variant, hue);

        // FontColorOverride rather than Modulate: Modulate multiplies the control *and its stylebox*
        // at draw time, so using it to carry a colour repaints the fill too.
        button.Label.FontColorOverride = variant == Variant.Neutral
            ? CrtTerminalPalette.Text
            : hue;

        // Centring a label takes both of these - AlignMode centres the text inside the Label's own
        // box, and HorizontalExpand is what makes that box span the button. The second is a plain
        // property and cannot come from a stylesheet rule.
        button.Label.HorizontalExpand = true;
        button.Label.Align = Label.AlignMode.Center;
    }

    private static Color HueOf(Variant variant)
    {
        return variant switch
        {
            Variant.Affirm => CrtTerminalPalette.Accent,
            Variant.Caution => CrtTerminalPalette.Caution,
            Variant.Danger => CrtTerminalPalette.Alert,
            _ => CrtTerminalPalette.Text,
        };
    }

    private static StyleBox MakeBox(Variant variant, Color hue)
    {
        // Neutral sits on the ladder rung buttons already use; the rest are that same rung rotated to
        // their own hue, so nothing in a row is heavier than anything else.
        var fill = variant == Variant.Neutral
            ? CrtTerminalPalette.Surface2
            : CrtTerminalPalette.ChatRowTint(hue, CrtTerminalPalette.ChatTintSaturationFull);

        return new CrtStyleBox
        {
            BackgroundColor = fill,
            BorderColor = hue,
            // Bordered only in base mode, where there is no ladder to separate a button from its
            // ground. Under CRT the fill does that work and a border would be the box-in-box again.
            BorderThickness = StyleNano.CrtUiEnabled ? new Thickness(0) : new Thickness(1),
            DrawCornerTicks = false,
            ContentMarginLeftOverride = PadHorizontal,
            ContentMarginRightOverride = PadHorizontal,
            ContentMarginTopOverride = PadTop,
            ContentMarginBottomOverride = PadBottom,
        };
    }
}
