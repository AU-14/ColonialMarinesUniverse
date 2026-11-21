using Content.Client.CMU14.Interface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.CorporateWork;

/// <summary>
///     Shared layout pieces for the corporate work terminals, so they read the same: a shaded
///     work panel, and striped rows that stay apart at a glance.
/// </summary>
public static class CMUTerminalLayout
{
    /// <summary>
    ///     Gives a panel the fill one step up from the window, with room inside.
    /// </summary>
    public static void StylePanel(PanelContainer panel)
    {
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = CrtTerminalPalette.Surface1,
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
        };
    }

    /// <summary>
    ///     Wraps a row in a stripe, alternating with its neighbours.
    /// </summary>
    public static PanelContainer Row(Control content, int index)
    {
        var row = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = index % 2 == 0 ? CrtTerminalPalette.Surface2 : CrtTerminalPalette.Surface1,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            },
        };
        row.AddChild(content);
        return row;
    }
}
