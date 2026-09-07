using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

[Virtual]
public class ChatInputBox : PanelContainer
{
    public const string StyleClassChatPanel = "ChatPanel";
    public const string StyleClassChatLineEdit = "ChatLineEdit";
    public const string StyleClassChatFilterOptionButton = "ChatFilterOptionButton";

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
            // 12 under CRT, matching the gallery's gap. At 2 the placeholder text started
            // immediately against the channel chip and the two read as one run-on control.
            SeparationOverride = StyleNano.CrtUiEnabled ? 12 : 2,
            Margin = new Thickness(0)
        };
        AddChild(Container);

        ChannelSelector = new ChannelSelectorButton
        {
            Name = "ChannelSelector",
            ToggleMode = true,
            StyleClasses = { ChannelSelectorItemButton.StyleClassChatSelectorOptionButton },
            // Under CRT the chip sizes to its own label - a fixed 74 stretched three letters into a
            // slab. The bar's height comes from CrtChatInput's 8px padding there, so neither
            // minimum is load-bearing any more.
            MinWidth = StyleNano.CrtUiEnabled ? 0 : 74,
            // The row is only as tall as its tallest child, and the CRT button box this now uses is
            // much shorter than the NanoUI one, so this is what holds the input bar open.
            MinHeight = StyleNano.CrtUiEnabled ? 0 : 26
        };
        Container.AddChild(ChannelSelector);
        Input = new HistoryLineEdit
        {
            Name = "Input",
            PlaceHolder = GetChatboxInfoPlaceholder(),
            HorizontalExpand = true,
            StyleClasses = { StyleClassChatLineEdit }
        };
        Container.AddChild(Input);
        FilterButton = new ChannelFilterButton
        {
            Name = "FilterButton",
            StyleClasses = { StyleClassChatFilterOptionButton },
            MinSize = new Vector2(28, 26)
        };
        Container.AddChild(FilterButton);
        AddStyleClass(StyleClassChatPanel);
        ChannelSelector.OnChannelSelect += UpdateActiveChannel;
    }

    public void SetLegacyMode(bool legacy)
    {
        // Mirrors the constructor: the CRT chip sizes to its label and takes the gallery's 12px gap.
        Container.SeparationOverride = legacy ? 4 : (StyleNano.CrtUiEnabled ? 12 : 2);
        Container.Margin = new Thickness(0);
        ChannelSelector.MinWidth = legacy ? 75 : (StyleNano.CrtUiEnabled ? 0 : 74);
        ChannelSelector.MinHeight = legacy || StyleNano.CrtUiEnabled ? 0 : 26;
        FilterButton.MinSize = legacy ? Vector2.Zero : new Vector2(28, 26);
        FilterButton.SetLegacyMode(legacy);
    }

    private void UpdateActiveChannel(ChatSelectChannel selectedChannel)
    {
        ActiveChannel = (ChatChannel) selectedChannel;
    }

    private static string GetChatboxInfoPlaceholder()
    {
        return (BoundKeyHelper.IsBound(ContentKeyFunctions.FocusChat),
                BoundKeyHelper.IsBound(ContentKeyFunctions.CycleChatChannelForward)) switch
            {
                (true, true) => Loc.GetString("hud-chatbox-info",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat)),
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (true, false) => Loc.GetString("hud-chatbox-info-talk",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat))),
                (false, true) => Loc.GetString("hud-chatbox-info-cycle",
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (false, false) => Loc.GetString("hud-chatbox-info-unbound")
            };
    }
}
