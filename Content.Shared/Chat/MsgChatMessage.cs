using System.IO;
using JetBrains.Annotations;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Chat
{
    [Serializable, NetSerializable]
    public enum ChatDisplayKind : byte
    {
        Unknown,
        Local,
        Whisper,
        Emote,
        Radio,
        LOOC,
        OOC,
        Dead,
        Admin,
        Mentor,
        System,
        Combat
    }

    [Serializable, NetSerializable]
    public sealed class ChatDisplayMetadata
    {
        public ChatDisplayKind Kind;
        public string? SenderName;
        public string? SenderPrefix;
        public string? Verb;
        public string? ChannelLabel;
        public bool QuoteBody;
        public Color? AccentColor;

        public ChatDisplayMetadata(
            ChatDisplayKind kind,
            string? senderName = null,
            string? senderPrefix = null,
            string? verb = null,
            string? channelLabel = null,
            bool quoteBody = false,
            Color? accentColor = null)
        {
            Kind = kind;
            SenderName = senderName;
            SenderPrefix = senderPrefix;
            Verb = verb;
            ChannelLabel = channelLabel;
            QuoteBody = quoteBody;
            AccentColor = accentColor;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ChatMessage
    {
        public ChatChannel Channel;

        /// <summary>
        /// This is the text spoken by the entity, after accents and such were applied.
        /// This should have <see cref="FormattedMessage.EscapeText"/> applied before using it in any rich text box.
        /// </summary>
        public string Message;

        /// <summary>
        /// This is the <see cref="Message"/> but with special characters escaped and wrapped in some rich text
        /// formatting tags.
        /// </summary>
        public string WrappedMessage;

        public NetEntity SenderEntity;

        /// <summary>
        ///     Identifier sent when <see cref="SenderEntity"/> is <see cref="NetEntity.Invalid"/>
        ///     if this was sent by a player to assign a key to the sender of this message.
        ///     This is unique per sender.
        /// </summary>
        public int? SenderKey;

        public bool HideChat;
        public Color? MessageColorOverride;
        public string? AudioPath;
        public float AudioVolume;
        public ChatDisplayMetadata? Display;

        // RMC14
        public bool HidePopup;
        public string? SpeechStyleClass;
        public bool RepeatCheckSender;

        [NonSerialized]
        public bool Read;

        public ChatMessage(ChatChannel channel, string message, string wrappedMessage, NetEntity source, int? senderKey, bool hideChat = false, Color? colorOverride = null, string? audioPath = null, float audioVolume = 0, bool hidePopup = false, string? speechStyleClass = null, bool repeatCheckSender = true, ChatDisplayMetadata? display = null)
        {
            Channel = channel;
            Message = message;
            WrappedMessage = wrappedMessage;
            SenderEntity = source;
            SenderKey = senderKey;
            HideChat = hideChat;
            MessageColorOverride = colorOverride;
            AudioPath = audioPath;
            AudioVolume = audioVolume;
            HidePopup = hidePopup;
            SpeechStyleClass = speechStyleClass;
            RepeatCheckSender = repeatCheckSender;
            Display = display ?? CreateDefaultDisplay(channel);
        }

        private static ChatDisplayMetadata CreateDefaultDisplay(ChatChannel channel)
        {
            return new ChatDisplayMetadata(channel switch
            {
                ChatChannel.Local => ChatDisplayKind.Local,
                ChatChannel.Whisper => ChatDisplayKind.Whisper,
                ChatChannel.Emotes => ChatDisplayKind.Emote,
                ChatChannel.Radio => ChatDisplayKind.Radio,
                ChatChannel.LOOC => ChatDisplayKind.LOOC,
                ChatChannel.OOC => ChatDisplayKind.OOC,
                ChatChannel.Dead => ChatDisplayKind.Dead,
                ChatChannel.Admin or ChatChannel.AdminAlert or ChatChannel.AdminChat => ChatDisplayKind.Admin,
                ChatChannel.MentorChat => ChatDisplayKind.Mentor,
                ChatChannel.Server or ChatChannel.Notifications => ChatDisplayKind.System,
                ChatChannel.Damage or ChatChannel.Visual => ChatDisplayKind.Combat,
                _ => ChatDisplayKind.Unknown
            }, channelLabel: channel switch
            {
                ChatChannel.Local => "SAY",
                ChatChannel.Whisper => "WHSP",
                ChatChannel.Emotes => "ME",
                ChatChannel.Radio => "RAD",
                ChatChannel.LOOC => "LOOC",
                ChatChannel.OOC => "OOC",
                ChatChannel.Dead => "DEAD",
                ChatChannel.Admin => "ADMIN",
                ChatChannel.AdminAlert => "ALERT",
                ChatChannel.AdminChat => "ASAY",
                ChatChannel.MentorChat => "MENTOR",
                ChatChannel.Notifications => "NOTE",
                ChatChannel.Server => "SYS",
                ChatChannel.Damage => "DMG",
                ChatChannel.Visual => "VIS",
                _ => "CHAT"
            });
        }
    }

    /// <summary>
    ///     Sent from server to client to notify the client about a new chat message.
    /// </summary>
    [UsedImplicitly]
    public sealed class MsgChatMessage : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public ChatMessage Message = default!;

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            var length = buffer.ReadVariableInt32();
            using var stream = new MemoryStream(length);
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out Message);
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            var stream = new MemoryStream();
            serializer.SerializeDirect(stream, Message);
            buffer.WriteVariableInt32((int) stream.Length);
            buffer.Write(stream.AsSpan());
        }
    }
}
