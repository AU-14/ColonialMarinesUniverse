using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     The lobby ready toggle's two looks, in one place.
/// </summary>
/// <remarks>
///     <para>
///     Shared because it has to be. The real control only exists before a round starts, so it is
///     normally looked at through the <c>cmu.panel_preview=ready</c> harness - and a harness that
///     styles the button by its own copy of the rules is not showing you the control, it is showing
///     you the copy. That is exactly what happened: the preview kept putting the off class on both
///     buttons and rendering the lit state as a hover, which measured Surface2 where the real thing
///     would have been lit.
///     </para>
///     <para>
///     Three channels carry the state, and they are meant to be redundant: the fill inverts, the
///     leading edge changes colour, and the label gains slashes. Any one of them alone would be a
///     detail someone could miss on a lobby they are not looking directly at.
///     </para>
/// </remarks>
public static class CmuReadyToggle
{
    /// <summary>
    ///     Put <paramref name="button"/> into the look for <paramref name="ready"/>.
    /// </summary>
    /// <remarks>
    ///     Swaps between two classes rather than keying the lit state off the Pressed pseudo-class.
    ///     A bare class rule and a Pressed rule match the same control at the same specificity, and
    ///     that has no defined winner - the first attempt at this rendered both states identically,
    ///     which only turned up because the pixels were sampled rather than eyeballed.
    /// </remarks>
    public static void Apply(Button button, bool ready)
    {
        var wanted = ready
            ? StyleNano.StyleClassCrtReadyToggleOn
            : StyleNano.StyleClassCrtReadyToggle;

        var unwanted = ready
            ? StyleNano.StyleClassCrtReadyToggle
            : StyleNano.StyleClassCrtReadyToggleOn;

        button.RemoveStyleClass(unwanted);

        if (!button.HasStyleClass(wanted))
            button.AddStyleClass(wanted);

        button.Text = Loc.GetString(ready ? "cmu-lobby-ready-yes" : "cmu-lobby-ready-no");

        // Dark on the lit fill, dim on the inert one. A plain property, so it beats any rule and
        // cannot be lost to the specificity problem above.
        button.Label.FontColorOverride = ready
            ? CrtTerminalPalette.Surface0
            : CrtTerminalPalette.TextDim;

        button.Label.HorizontalExpand = true;

        if (!button.Label.HasStyleClass(StyleNano.StyleClassCrtButtonLabel))
            button.Label.AddStyleClass(StyleNano.StyleClassCrtButtonLabel);
    }
}
