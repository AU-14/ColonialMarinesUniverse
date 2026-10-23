using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     The semantic button set: one padding and one height across all of them, differing only in
///     fill and text tone.
/// </summary>
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
            : CrtTerminalPalette.ControlTint(hue);

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
