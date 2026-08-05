using System.Numerics;
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
        MinHeight = 25;
        ToolTip = Loc.GetString("hud-chatbox-split-tooltip");
        StyleClasses.Add(StyleNano.StyleClassChatChannelSelectorButton);
    }

    public void SetSplitState(bool enabled, string? tabTitle)
    {
        Text = enabled && !string.IsNullOrWhiteSpace(tabTitle)
            ? $"{Loc.GetString("hud-chatbox-split-toggle")} {tabTitle}"
            : $"{Loc.GetString("hud-chatbox-split-toggle")} +";
        Modulate = enabled ? Color.FromHex("#9fd0b3") : Color.FromHex("#737987");
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
