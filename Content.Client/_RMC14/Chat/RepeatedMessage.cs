using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Chat;

public sealed class RepeatedMessage
{
    public readonly ChatMessageRow? Row;
    public readonly int Index;
    public readonly FormattedMessage FormattedMessage;
    public readonly NetEntity SenderEntity;
    public readonly string Message;
    public readonly ChatChannel Channel;
    public readonly string? LanguageIcon;

    public int Count = 1;

    public RepeatedMessage(
        ChatMessageRow row,
        FormattedMessage formattedMessage,
        NetEntity senderEntity,
        string message,
        ChatChannel channel,
        string? languageIcon = null)
    {
        Row = row;
        Index = -1;
        FormattedMessage = formattedMessage;
        SenderEntity = senderEntity;
        Message = message;
        Channel = channel;
        LanguageIcon = languageIcon;
    }

    public RepeatedMessage(
        int index,
        FormattedMessage formattedMessage,
        NetEntity senderEntity,
        string message,
        ChatChannel channel,
        string? languageIcon = null)
    {
        Index = index;
        FormattedMessage = formattedMessage;
        SenderEntity = senderEntity;
        Message = message;
        Channel = channel;
        LanguageIcon = languageIcon;
    }
}