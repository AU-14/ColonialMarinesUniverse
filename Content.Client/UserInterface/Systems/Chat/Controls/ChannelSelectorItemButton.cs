using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChannelSelectorItemButton : Button
{
    public const string StyleClassChatSelectorOptionButton = "ChatSelectorOptionButton";


    public readonly ChatSelectChannel Channel;

    public bool IsHidden => Parent == null;

    public ChannelSelectorItemButton(ChatSelectChannel selector)
    {
        Channel = selector;
        AddStyleClass(StyleClassChatSelectorOptionButton);

        // Same treatment as ChatTabButton: these are built and rebuilt as selectable channels
        // change, so CrtLobbyTheme's one-shot tree walk never sees them.
        if (StyleNano.CrtUiEnabled)
        {
            AddStyleClass(StyleNano.StyleClassCrtButton);

            // Each entry wears its own channel colour, so the list is scannable by hue and the chip
            // that results from picking one is not the first time that colour is seen.
            Label.FontColorOverride = ChannelSelectorButton.CrtChannelColor(selector);
        }

        // The popup is a fixed fraction of the input row, so the buttons share that width evenly
        // instead of each sitting at its own text width.
        HorizontalExpand = true;
        MinHeight = 24;

        Text = ChannelSelectorButton.ChannelSelectorName(selector);

        var prefix = ChatUIController.ChannelPrefixes[selector];

        if (prefix != default)
            Text = Loc.GetString("hud-chatbox-select-name-prefixed", ("name", Text), ("prefix", prefix));
    }
}
