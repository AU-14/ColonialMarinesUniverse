using Content.Shared.Chat;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Radio;

public enum ANPRCNotice : byte
{
    /// <summary>What the set is doing: search progress, bearings, settings taking effect.</summary>
    Info,

    /// <summary>Something the operator worked for came good: a fix, a broken key.</summary>
    Good,

    /// <summary>The set refused, or something went wrong.</summary>
    Warn,
}

/// <summary>
///     Everything the 117G puts in the operator's chat, styled as radio rows rather than loose text:
///     the set's own notices under a 117G tag, traffic on a net the headset does not carry as the same
///     row the headset would have drawn, and intercepts under an INT tag of their own so an RTO can give
///     them a tab.
/// </summary>
public sealed partial class ANPRCChatSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    private static readonly Color InfoAccent = Color.FromHex("#7FC48C");
    private static readonly Color GoodAccent = Color.FromHex("#A6F0B0");
    private static readonly Color WarnAccent = Color.FromHex("#E3A94F");
    private static readonly Color InterceptAccent = Color.FromHex("#E09A3E");

    /// <summary>A line from the set itself.</summary>
    public void Notice(string text, EntityUid target, ANPRCNotice level = ANPRCNotice.Info)
    {
        if (!TryComp(target, out ActorComponent? actor))
            return;

        var accent = level switch
        {
            ANPRCNotice.Good => GoodAccent,
            ANPRCNotice.Warn => WarnAccent,
            _ => InfoAccent,
        };

        var label = Loc.GetString("anprc-chat-label");
        var body = FormattedMessage.EscapeText(text);
        var wrapped = level == ANPRCNotice.Good
            ? $"[color={accent.ToHex()}][bold]{body}[/bold][/color]"
            : $"[color={accent.ToHex()}]{body}[/color]";

        var chat = new ChatMessage(
            ChatChannel.Radio,
            text,
            wrapped,
            default,
            null,
            display: new ChatDisplayMetadata(
                ChatDisplayKind.System,
                channelLabel: label,
                accentColor: accent));

        _net.ServerSendMessage(new MsgChatMessage { Message = chat }, actor.PlayerSession.Channel);
    }

    /// <summary>
    ///     Traffic the pack heard on one of the operator's own nets that their headset does not carry:
    ///     the radio row exactly as a headset would draw it, with the body the pack actually heard.
    /// </summary>
    public void Traffic(ICommonSession session, ChatMessage original, string heard)
    {
        var body = FormattedMessage.EscapeText(heard);
        var wrapped = ReplaceLast(original.WrappedMessage, FormattedMessage.EscapeText(original.Message), body);

        var chat = new ChatMessage(
            original.Channel,
            heard,
            wrapped,
            original.SenderEntity,
            original.SenderKey,
            repeatCheckSender: false,
            languageIcon: original.LanguageIcon,
            display: original.Display);

        _net.ServerSendMessage(new MsgChatMessage { Message = chat }, session.Channel);
    }

    /// <summary>Traffic on somebody else's net, caught by the pack. Always under the INT tag.</summary>
    public void Intercept(ICommonSession session, EntityUid source, string sender, string net, string heard)
    {
        var label = Loc.GetString("anprc-chat-intercept-label");
        var wrapped = Loc.GetString(
            "anprc-chat-intercept-line",
            ("color", InterceptAccent.ToHex()),
            ("sender", FormattedMessage.EscapeText(sender)),
            ("net", FormattedMessage.EscapeText(net)),
            ("message", FormattedMessage.EscapeText(heard)));

        var chat = new ChatMessage(
            ChatChannel.Radio,
            heard,
            wrapped,
            GetNetEntity(source),
            null,
            repeatCheckSender: false,
            display: new ChatDisplayMetadata(
                ChatDisplayKind.Radio,
                senderName: sender,
                verb: Loc.GetString("anprc-chat-intercept-verb"),
                channelLabel: label,
                quoteBody: true,
                accentColor: InterceptAccent));

        _net.ServerSendMessage(new MsgChatMessage { Message = chat }, session.Channel);
    }

    // the body appears once in the wrap unless the sender's name contains it, so replace the last one
    private static string ReplaceLast(string text, string find, string replace)
    {
        if (string.IsNullOrEmpty(find))
            return text;

        var index = text.LastIndexOf(find, StringComparison.Ordinal);

        return index < 0
            ? text
            : text[..index] + replace + text[(index + find.Length)..];
    }
}
