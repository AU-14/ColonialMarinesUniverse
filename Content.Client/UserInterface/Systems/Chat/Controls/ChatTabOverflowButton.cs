using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatTabOverflowButton : ChatPopupButton<ChatTabOverflowPopup>
{
    public ChatTabOverflowButton()
    {
        Text = "+";
        MinWidth = 38;
        MinHeight = 25;
        Visible = false;
        ToolTip = Loc.GetString("hud-chatbox-tabs");
        StyleClasses.Add(StyleNano.StyleClassChatChannelSelectorButton);
    }

    public void SetHiddenCount(int count)
    {
        Visible = count > 0;
        Text = count > 0 ? $"+{count}" : "+";
        MinWidth = count > 9 ? 44 : 38;
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
