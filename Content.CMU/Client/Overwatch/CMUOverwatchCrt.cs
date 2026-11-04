using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// Puts the overwatch console on the CRT terminal look. The upstream layout paints its own grey and
/// blue panels and tints its buttons yellow and red; those are recoloured in place rather than
/// rewritten, so the console keeps upstream's structure and spacing.
/// </summary>
public static class CMUOverwatchCrt
{
    /// <summary>
    /// Buttons with this class keep their tint (the squad picker shows each squad's colour).
    /// </summary>
    public const string KeepTintClass = "CMUOverwatchKeepTint";

    private const string UpstreamButtonClass = "ActionButton";

    public static void Apply(Control root)
    {
        if (!StyleNano.CrtUiEnabled)
            return;

        Recolor(root);
        CrtLobbyTheme.Apply(root, useCrtTypography: true);
    }

    private static void Recolor(Control control)
    {
        switch (control)
        {
            case PanelContainer { PanelOverride: StyleBoxFlat flat }:
                if (flat.BackgroundColor.A > 0)
                    flat.BackgroundColor = CrtTerminalPalette.Surface1;

                flat.BorderColor = CrtTerminalPalette.Line;
                break;
            case Button button when !button.HasStyleClass(KeepTintClass):
                button.ModulateSelfOverride = null;
                button.Label.ModulateSelfOverride = null;
                if (button.HasStyleClass(UpstreamButtonClass))
                    button.RemoveStyleClass(UpstreamButtonClass);
                break;
        }

        foreach (var child in control.Children)
        {
            Recolor(child);
        }
    }

    /// <summary>
    /// Lights a tab or toggle button with the lobby ready toggle's two looks, so the selected one
    /// reads at a glance instead of being a slightly lighter grey.
    /// </summary>
    public static void SetLit(Button button, bool lit)
    {
        if (!StyleNano.CrtUiEnabled)
        {
            button.Pressed = lit;
            return;
        }

        var wanted = lit ? StyleNano.StyleClassCrtReadyToggleOn : StyleNano.StyleClassCrtReadyToggle;
        var unwanted = lit ? StyleNano.StyleClassCrtReadyToggle : StyleNano.StyleClassCrtReadyToggleOn;

        button.RemoveStyleClass(unwanted);
        button.RemoveStyleClass(StyleNano.StyleClassCrtButton);
        if (button.HasStyleClass(UpstreamButtonClass))
            button.RemoveStyleClass(UpstreamButtonClass);

        if (!button.HasStyleClass(wanted))
            button.AddStyleClass(wanted);

        button.ModulateSelfOverride = null;
        button.Label.ModulateSelfOverride = null;
        button.Label.FontColorOverride = lit ? CrtTerminalPalette.Surface0 : CrtTerminalPalette.TextDim;
        button.Label.HorizontalExpand = true;
        if (!button.Label.HasStyleClass(StyleNano.StyleClassCrtButtonLabel))
            button.Label.AddStyleClass(StyleNano.StyleClassCrtButtonLabel);
    }
}
