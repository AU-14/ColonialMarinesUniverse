using System.Linq;
using System.Text;
using Content.Server._RMC14.Chat.Chat;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chat;
using Content.Shared.Ghost.Components;
using Content.Shared.Players;
using Content.Shared.Speech.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private enum MessageRangeCheckResult
    {
        Disallowed,
        HideChat,
        Full,
    }

    /// <summary>
    /// Whether replay recipients should have the message hidden from chat.
    /// </summary>
    private bool MessageRangeHideChatForReplay(ChatTransmitRange range)
    {
        return range == ChatTransmitRange.HideChat;
    }

    private MessageRangeCheckResult MessageRangeCheck(
        ICommonSession session,
        ICChatRecipientData data,
        ChatTransmitRange range)
    {
        var initialResult = range switch
        {
            ChatTransmitRange.Normal => MessageRangeCheckResult.Full,
            ChatTransmitRange.GhostRangeLimit => data.Observer && data.Range < 0 && !_adminManager.IsAdmin(session)
                ? MessageRangeCheckResult.HideChat
                : MessageRangeCheckResult.Full,
            ChatTransmitRange.HideChat => MessageRangeCheckResult.HideChat,
            ChatTransmitRange.NoGhosts => data.Observer && !_adminManager.IsAdmin(session)
                ? MessageRangeCheckResult.Disallowed
                : MessageRangeCheckResult.Full,
            _ => MessageRangeCheckResult.Full,
        };

        var insistHideChat = data.HideChatOverride ?? false;
        var insistNoHideChat = !(data.HideChatOverride ?? true);
        if (insistHideChat && initialResult == MessageRangeCheckResult.Full)
            return MessageRangeCheckResult.HideChat;
        if (insistNoHideChat && initialResult == MessageRangeCheckResult.HideChat)
            return MessageRangeCheckResult.Full;
        return initialResult;
    }

    /// <summary>
    /// Sends a chat message to players in voice range of the source.
    /// </summary>
    private void SendInVoiceRange(
        ChatChannel channel,
        string message,
        string wrappedMessage,
        EntityUid source,
        ChatTransmitRange range,
        NetUserId? author = null,
        string? speechStyleClass = null)
    {
        foreach (var (session, data) in GetRecipients(source, VoiceRange))
        {
            if ((channel == ChatChannel.Local || channel == ChatChannel.Emotes) &&
                !CanHearYautjaLocalSpeech(source, session, data))
            {
                continue;
            }

            var entRange = MessageRangeCheck(session, data, range);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;

            var ev = new ChatMessageOverrideInVoiceRangeEvent(
                session,
                channel,
                source,
                message,
                wrappedMessage,
                entRange == MessageRangeCheckResult.HideChat);

            if (session.AttachedEntity is { Valid: true } listener)
                RaiseLocalEvent(listener, ref ev);
            else
                RaiseLocalEvent(source, ref ev);

            _chatManager.ChatMessageToOne(
                channel,
                ev.Message,
                GetYautjaVisibleWrappedMessage(ev.WrappedMessage, source, session),
                source,
                ev.EntHideChat,
                session.Channel,
                author: author,
                speechStyleClass: speechStyleClass);
        }

        _replay.RecordServerMessage(
            new ChatMessage(
                channel,
                message,
                wrappedMessage,
                GetNetEntity(source),
                null,
                MessageRangeHideChatForReplay(range),
                speechStyleClass: speechStyleClass ??
                                  CompOrNull<RMCSpeechBubbleSpecificStyleComponent>(source)?.SpeechStyleClass,
                repeatCheckSender: !HasComp<ChatRepeatIgnoreSenderComponent>(source)));
    }

    private bool CanSendInGame(
        string message,
        IConsoleShell? shell = null,
        ICommonSession? player = null)
    {
        if (player == null)
            return true;

        if (player.ContentData()?.Mind == null)
        {
            shell?.WriteError("You don't have a mind!");
            return false;
        }

        if (player.AttachedEntity is not { Valid: true })
        {
            shell?.WriteError("You don't have an entity!");
            return false;
        }

        return !_chatManager.MessageCharacterLimit(player, message);
    }

    // ReSharper disable once InconsistentNaming
    private string SanitizeInGameICMessage(
        EntityUid source,
        string message,
        out string? emoteStr,
        bool capitalize = true,
        bool punctuate = false,
        bool capitalizeTheWordI = true,
        bool skipEmoteShorthands = false)
    {
        var newMessage = SanitizeMessageReplaceWords(source, message.Trim());

        GetRadioKeycodePrefix(source, newMessage, out newMessage, out var prefix);

        if (capitalize)
            newMessage = SanitizeMessageCapital(newMessage);
        if (capitalizeTheWordI)
            newMessage = SanitizeMessageCapitalizeTheWordI(newMessage, "i");
        if (punctuate)
            newMessage = SanitizeMessagePeriod(newMessage);

        if (!skipEmoteShorthands)
            _sanitizer.TrySanitizeEmoteShorthands(newMessage, source, out newMessage, out emoteStr);
        else
            emoteStr = null;

        return prefix + newMessage;
    }

    private string SanitizeInGameOOCMessage(string message)
    {
        return FormattedMessage.EscapeText(message.Trim());
    }

    public string TransformSpeech(EntityUid sender, string message)
    {
        var ev = new TransformSpeechEvent(sender, message);
        RaiseLocalEvent(sender, ev, true);
        return ev.Message;
    }

    public bool CheckIgnoreSpeechBlocker(EntityUid sender, bool ignoreBlocker)
    {
        if (ignoreBlocker)
            return true;

        var ev = new CheckIgnoreSpeechBlockerEvent(sender, false);
        RaiseLocalEvent(sender, ev, true);
        return ev.IgnoreBlocker;
    }

    private IEnumerable<INetChannel> GetDeadChatClients()
    {
        return Filter.Empty()
            .AddWhereAttachedEntity(HasComp<GhostComponent>)
            .Recipients
            .Union(_adminManager.ActiveAdmins)
            .Select(player => player.Channel);
    }

    private static string SanitizeMessagePeriod(string message)
    {
        if (!string.IsNullOrEmpty(message) && char.IsLetter(message[^1]))
            message += ".";
        return message;
    }

    public static readonly ProtoId<ReplacementAccentPrototype> ChatSanitize_Accent = "chatsanitize";

    public string SanitizeMessageReplaceWords(EntityUid source, string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var replaced = _wordreplacement.ApplyReplacements(message, ChatSanitize_Accent);
        return _cmChat.SanitizeMessageReplaceWords(source, replaced);
    }

    /// <summary>
    /// Returns in-range players and observers, with observer range set to -1 when out of range.
    /// </summary>
    private Dictionary<ICommonSession, ICChatRecipientData> GetRecipients(
        EntityUid source,
        float voiceGetRange,
        bool ignoreXenos = false)
    {
        var recipients = new Dictionary<ICommonSession, ICChatRecipientData>();
        var transformSource = Transform(source);
        var sourceMapId = transformSource.MapID;
        var sourceCoords = transformSource.Coordinates;

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = Transform(playerEntity);
            if (transformEntity.MapID != sourceMapId)
                continue;

            var observer = _ghostHearingQuery.HasComponent(playerEntity);
            if (sourceCoords.TryDistance(EntityManager, transformEntity.Coordinates, out var distance) &&
                distance < voiceGetRange)
            {
                var hasLos = observer || _examineSystem.InRangeUnOccluded(source, playerEntity, voiceGetRange);
                recipients.Add(player, new ICChatRecipientData(distance, observer, HasLOS: hasLos));
                continue;
            }

            if (observer)
            {
                var hasLos = _examineSystem.InRangeUnOccluded(source, playerEntity, voiceGetRange);
                recipients.Add(player, new ICChatRecipientData(-1, true, HasLOS: hasLos));
            }
        }

        RaiseLocalEvent(new ExpandICChatRecipientsEvent(source, voiceGetRange, recipients));

        var ev = new ChatMessageAfterGetRecipients(recipients);
        RaiseLocalEvent(source, ref ev);

        if (ignoreXenos)
        {
            foreach (var session in recipients.Keys.ToArray())
            {
                if (session.AttachedEntity is { Valid: true } listener && HasComp<XenoComponent>(listener))
                    recipients.Remove(session);
            }
        }

        return recipients;
    }

    public readonly record struct ICChatRecipientData(
        float Range,
        bool Observer,
        bool? HideChatOverride = null,
        bool HasLOS = true);

    private string ObfuscateMessageReadability(string message, float chance)
    {
        var modifiedMessage = new StringBuilder(message);
        for (var i = 0; i < message.Length; i++)
        {
            if (char.IsWhiteSpace(modifiedMessage[i]))
                continue;

            if (_random.Prob(1 - chance))
                modifiedMessage[i] = '~';
        }

        return modifiedMessage.ToString();
    }

    public string BuildGibberishString(IReadOnlyList<char> charOptions, int length)
    {
        var result = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            result.Append(_random.Pick(charOptions));
        }

        return result.ToString();
    }

    private string GetYautjaVisibleWrappedMessage(
        string wrappedMessage,
        EntityUid source,
        ICommonSession session)
    {
        if (!TryGetYautjaVisibleName(source, session, out var visibleName))
            return wrappedMessage;

        var hiddenName = FormattedMessage.EscapeText(Loc.GetString(Comp<YautjaComponent>(source).IdentityName));
        return ReplaceFirst(wrappedMessage, hiddenName, visibleName);
    }

    private bool TryGetYautjaVisibleName(
        EntityUid source,
        ICommonSession session,
        out string visibleName)
    {
        visibleName = string.Empty;
        if (!HasComp<YautjaComponent>(source) ||
            session.AttachedEntity is not { Valid: true } listener ||
            !HasComp<YautjaComponent>(listener))
        {
            return false;
        }

        visibleName = FormattedMessage.EscapeText(MetaData(source).EntityName);
        return true;
    }

    private static string ReplaceFirst(string value, string search, string replacement)
    {
        if (string.IsNullOrEmpty(search))
            return value;

        var index = value.IndexOf(search, StringComparison.Ordinal);
        if (index < 0)
            index = value.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return value;

        return value[..index] + replacement + value[(index + search.Length)..];
    }

    private bool CanHearYautjaLocalSpeech(
        EntityUid source,
        ICommonSession session,
        ICChatRecipientData data)
    {
        if (!HasComp<YautjaComponent>(source) || data.Observer)
            return true;

        if (session.AttachedEntity is not { Valid: true } listener)
            return false;

        return listener == source ||
               HasComp<YautjaComponent>(listener) ||
               HasComp<YautjaThrallComponent>(listener) ||
               HasComp<YautjaHivebrokenXenoComponent>(listener);
    }
}
