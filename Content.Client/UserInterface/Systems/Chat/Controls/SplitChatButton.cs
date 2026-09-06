using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class SplitChatButton : ChatPopupButton<SplitChatPopup>
{
    public SplitChatButton()
    {
        Text = $"{Loc.GetString("hud-chatbox-split-toggle")} +";
        MinWidth = 66;
        MinHeight = 26;
        ToolTip = Loc.GetString("hud-chatbox-split-tooltip");
        StyleClasses.Add(StyleNano.StyleClassChatChannelSelectorButton);

        // The tab class, not CrtButton. It sits on the tab strip, and a resting tab draws no fill
        // now - a filled CrtButton here would be the only box on the row and would read as the
        // selected tab. Set here rather than through CrtLobbyTheme, which returns early on a ChatBox
        // and never walks the chat's controls.
        if (StyleNano.CrtUiEnabled)
            AddStyleClass(StyleNano.StyleClassCrtChatTab);
    }

    public void SetSplitState(bool enabled, string? tabTitle)
    {
        Text = enabled && !string.IsNullOrWhiteSpace(tabTitle)
            ? $"{Loc.GetString("hud-chatbox-split-toggle")} {tabTitle}"
            : $"{Loc.GetString("hud-chatbox-split-toggle")} +";
        // Colour the label, not the control: Modulate multiplies the stylebox too, and the disabled
        // grey was dragging the CRT button's Surface2 fill down to a near-black chip that read as a
        // framed box on the tab strip. See the same fix in ChatBox.UpdateTabButtons.
        if (StyleNano.CrtUiEnabled)
        {
            Modulate = Color.White;
            Label.FontColorOverride = enabled
                ? CrtTerminalPalette.TextBright
                : CrtTerminalPalette.TextDim;
        }
        else
        {
            Modulate = enabled ? Color.White : Color.FromHex("#737987");
        }

        MinWidth = enabled && tabTitle != null ? Math.Max(92, 54 + tabTitle.Length * 8) : 66;
    }

    protected override UIBox2 GetPopupPosition()
    {
        var globalPos = GlobalPosition;
        var (minX, minY) = Popup.MinSize;
        var width = Math.Max(minX, Popup.MinWidth);
        return UIBox2.FromDimensions(
            globalPos - new Vector2(width - Width, minY + 4),
            new Vector2(width, minY));
    }
}
