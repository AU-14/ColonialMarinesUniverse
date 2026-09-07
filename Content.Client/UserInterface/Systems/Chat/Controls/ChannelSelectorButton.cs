using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChannelSelectorButton : ChatPopupButton<ChannelSelectorPopup>
{
    public event Action<ChatSelectChannel>? OnChannelSelect;

    public ChatSelectChannel SelectedChannel { get; private set; }

    /// <summary>
    ///     How much of the chat input row the channel popup spans, left-aligned with it.
    /// </summary>
    private const float PopupWidthFraction = 0.5f;

    public ChannelSelectorButton()
    {
        Name = "ChannelSelector";

        // Match the channel buttons in the popup this opens. Without it this stayed a plain NanoUI
        // button sitting next to a strip of CRT ones. Set here rather than via CrtLobbyTheme, which
        // returns early on a ChatBox and so never walks the chat's controls at all.
        if (StyleNano.CrtUiEnabled)
        {
            // The chip's own class, not CrtButton. CrtButton is sized to be pressed (12x5), which
            // at three letters produced a slab filling most of the input bar's height - a box
            // inside a box. See StyleClassCrtChatChannelChip.
            AddStyleClass(StyleNano.StyleClassCrtChatChannelChip);

            // Matches the message bodies, whichever face those are currently using. RichTextLabel
            // takes its font only from the stylesheet, so the log is pinned there and this has to
            // meet it - at 12 against a log at 8 the prompt was half again the size of the text
            // beside it.
            // FontOverride rather than a style class: the CRT rule sizes every button label through
            // a single parent-child selector that a competing class would only tie with.
            RefreshChatFont();
        }

        Popup.Selected += OnChannelSelected;

        if (Popup.FirstChannel is { } firstSelector)
        {
            Select(firstSelector);
        }
    }

    /// <summary>
    ///     Re-read the chat font. Called when the readable-font option changes: a FontOverride set at
    ///     construction survives a stylesheet rebuild, so the prompt would otherwise be the one thing
    ///     on the row still in the old face.
    /// </summary>
    public void RefreshChatFont()
    {
        Label.FontOverride = StyleNano.CrtUiEnabled
            ? StyleNano.GetChatFont(IoCManager.Resolve<IResourceCache>())
            : null;
    }

    protected override UIBox2 GetPopupPosition()
    {
        // Sit on top of the whole input row, not below this button. The input row is the bottom
        // edge of the chat, so opening downwards put the list off the panel entirely - and the old
        // box was only this button's width, so the channels spilled out of it.
        //
        // Anchor to the ChatInputBox itself rather than to Parent: Parent is the BoxContainer
        // *inside* it, which is already inset by the input row's stylebox margins.
        var row = FindInputRow();

        // Measure before positioning: the top edge is derived from the popup's height, and
        // Popup.MeasureOverride floors at whatever size the previous Open() passed, so a guess made
        // here would stick permanently.
        Popup.Measure(Vector2Helpers.Infinity);

        // Half the input row, not all of it - but never narrower than the channels actually need,
        // so the in-round list (up to nine channels, against three or four in the lobby) still fits
        // rather than squeezing every label into a sliver.
        var width = MathF.Max(row.Width * PopupWidthFraction, Popup.DesiredSize.X);

        // Tall enough to cover the input row outright. The popup's bottom edge is the row's *bottom*
        // edge, so it sits over the bar rather than perching on top of it and leaving the channel
        // button and placeholder text peeking out underneath.
        var height = MathF.Max(Popup.DesiredSize.Y, row.Height);

        return UIBox2.FromDimensions(
            new Vector2(row.GlobalPosition.X, row.GlobalPosition.Y + row.Height - height),
            new Vector2(width, height));
    }

    private Control FindInputRow()
    {
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is ChatInputBox)
                return parent;
        }

        return Parent ?? this;
    }

    private void OnChannelSelected(ChatSelectChannel channel)
    {
        Select(channel);
    }

    public void Select(ChatSelectChannel channel)
    {
        if (Popup.Visible)
        {
            Popup.Close();
        }

        if (SelectedChannel == channel)
            return;
        SelectedChannel = channel;
        OnChannelSelect?.Invoke(channel);
    }

    public static string ChannelSelectorName(ChatSelectChannel channel)
    {
        return Loc.GetString($"hud-chatbox-select-channel-{channel}");
    }

    public Color ChannelSelectColor(ChatSelectChannel channel)
    {
        return channel switch
        {
            ChatSelectChannel.Radio => Color.LimeGreen,
            ChatSelectChannel.LOOC => Color.MediumTurquoise,
            ChatSelectChannel.OOC => Color.LightSkyBlue,
            ChatSelectChannel.Dead => Color.MediumPurple,
            ChatSelectChannel.Admin => Color.HotPink,
            ChatSelectChannel.Mentor => Color.Orange,
            _ => Color.DarkGray
        };
    }

    /// <summary>
    ///     The channel's colour under the CRT theme: the hue above, rebuilt at the channel band's
    ///     fixed luminance so nine channels are tellable apart without any of them glaring.
    /// </summary>
    /// <remarks>
    ///     Local and Whisper stay on the ladder rather than taking a hue: they are where a player
    ///     spends most of their time, and a hue there would mean the chip never stops shouting.
    /// </remarks>
    public static Color CrtChannelColor(ChatSelectChannel channel)
    {
        return channel switch
        {
            ChatSelectChannel.Local => CrtTerminalPalette.Text,
            ChatSelectChannel.Whisper => CrtTerminalPalette.TextDim,
            ChatSelectChannel.Radio => CrtTerminalPalette.Accent,
            ChatSelectChannel.Emotes => CrtTerminalPalette.ChannelTone(Color.FromHex("#C9A7EA")),
            ChatSelectChannel.LOOC => CrtTerminalPalette.ChannelTone(Color.FromHex("#4FD2C2")),
            ChatSelectChannel.OOC => CrtTerminalPalette.ChannelTone(Color.FromHex("#73BDF6")),
            ChatSelectChannel.Dead => CrtTerminalPalette.ChannelTone(Color.FromHex("#8D7BD4")),
            // Admin and Mentor keep the palette's own severity tones rather than a rebuilt hue: they
            // are the two that mean "staff are involved", which is exactly what Alert and Caution
            // already say everywhere else in the theme.
            ChatSelectChannel.Admin => CrtTerminalPalette.Alert,
            ChatSelectChannel.Mentor => CrtTerminalPalette.Caution,
            _ => CrtTerminalPalette.Text,
        };
    }

    public void UpdateChannelSelectButton(ChatSelectChannel channel, RadioChannelPrototype? radio)
    {
        Text = radio != null ? Loc.GetString(radio.Name) : ChannelSelectorName(channel);
        var channelColor = radio?.Color ?? ChannelSelectColor(channel);

        // Modulate multiplies the stylebox as well as the label, so a channel colour applied that
        // way repaints the chip's fill too; FontColorOverride touches only the text. A radio
        // channel's own prototype colour is pinned to the band's luminance rather than replaced.
        if (StyleNano.CrtUiEnabled)
        {
            Modulate = Color.White;
            Label.FontColorOverride = radio != null
                ? CrtTerminalPalette.ChannelTone(radio.Color)
                : CrtChannelColor(channel);
        }
        else
        {
            Modulate = channelColor;
        }
    }
}
