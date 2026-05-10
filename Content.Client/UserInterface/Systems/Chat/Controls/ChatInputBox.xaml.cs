using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

[Virtual]
public class ChatInputBox : PanelContainer
{
    public readonly ChannelSelectorButton ChannelSelector;
    public readonly HistoryLineEdit Input;
    public readonly ChannelFilterButton FilterButton;
    protected readonly BoxContainer Container;
    protected ChatChannel ActiveChannel { get; private set; } = ChatChannel.Local;

    public ChatInputBox()
    {
        Container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            Margin = new Thickness(0)
        };
        AddChild(Container);

        ChannelSelector = new ChannelSelectorButton
        {
            Name = "ChannelSelector",
            ToggleMode = true,
            StyleClasses = {"chatSelectorOptionButton"},
            MinWidth = 74,
            MinHeight = 20
        };
        Container.AddChild(ChannelSelector);
        Input = new HistoryLineEdit
        {
            Name = "Input",
            PlaceHolder = GetChatboxInfoPlaceholder(),
            HorizontalExpand = true,
            StyleClasses = {"chatLineEdit"}
        };
        Container.AddChild(Input);
        FilterButton = new ChannelFilterButton
        {
            Name = "FilterButton",
            StyleClasses = {"chatFilterOptionButton"},
            MinSize = new Vector2(22, 20)
        };
        Container.AddChild(FilterButton);
        AddStyleClass(StyleNano.StyleClassChatSubPanel);
        ChannelSelector.OnChannelSelect += UpdateActiveChannel;
    }

    public void SetLegacyMode(bool legacy)
    {
        Container.SeparationOverride = legacy ? 4 : 2;
        Container.Margin = new Thickness(0);
        ChannelSelector.MinWidth = legacy ? 75 : 74;
        ChannelSelector.MinHeight = legacy ? 0 : 20;
        FilterButton.MinSize = legacy ? Vector2.Zero : new Vector2(22, 20);
        FilterButton.SetLegacyMode(legacy);
    }

    private void UpdateActiveChannel(ChatSelectChannel selectedChannel)
    {
        ActiveChannel = (ChatChannel) selectedChannel;
    }

    private static string GetChatboxInfoPlaceholder()
    {
        return (BoundKeyHelper.IsBound(ContentKeyFunctions.FocusChat), BoundKeyHelper.IsBound(ContentKeyFunctions.CycleChatChannelForward)) switch
        {
            (true, true) => Loc.GetString("hud-chatbox-info", ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat)), ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
            (true, false) => Loc.GetString("hud-chatbox-info-talk", ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat))),
            (false, true) => Loc.GetString("hud-chatbox-info-cycle", ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
            (false, false) => Loc.GetString("hud-chatbox-info-unbound")
        };
    }
}
