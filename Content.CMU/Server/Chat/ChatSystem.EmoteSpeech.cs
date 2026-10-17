using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server._RMC14.Chat.Chat;
using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

/// <summary>
/// Emotes with quoted speech: <c>walks over "I hate it here."</c>.
/// The quoted parts go through the same accents, traits and language as normal speech;
/// the rest of the emote is left alone.
/// </summary>
public sealed partial class ChatSystem
{
    private static readonly Regex CMUEmoteQuoteRegex = new("[\"“”]([^\"“”]*)[\"“”]", RegexOptions.Compiled);

    private readonly record struct CMUEmotePart(string Text, bool Speech);

    private bool TrySendCMUEmoteWithSpeech(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog,
        bool ignoreActionBlocker,
        ProtoId<LanguagePrototype> language)
    {
        var matches = CMUEmoteQuoteRegex.Matches(action);
        if (!matches.Any(m => !string.IsNullOrWhiteSpace(m.Groups[1].Value)))
            return false;

        if (!_actionBlocker.CanEmote(source) &&
            !ignoreActionBlocker &&
            !_mobStateSystem.IsCritical(source))
        {
            return true;
        }

        if (!_prototypeManager.TryIndex(language, out var languagePrototype))
        {
            language = SharedLanguageSystem.CommonLanguage;
            _prototypeManager.TryIndex(language, out languagePrototype);
        }

        var needsSpeech = languagePrototype?.NeedsSpeech ?? true;
        var needsLos = languagePrototype?.NeedsLOS ?? false;
        var canSpeak = !needsSpeech || ignoreActionBlocker || _actionBlocker.CanSpeak(source);

        var parts = new List<CMUEmotePart>();
        var spoken = new List<string>();
        var last = 0;
        foreach (Match match in matches)
        {
            if (match.Index > last)
                parts.Add(new CMUEmotePart(action[last..match.Index], false));
            last = match.Index + match.Length;

            var said = match.Groups[1].Value.Trim();
            if (said.Length == 0)
            {
                parts.Add(new CMUEmotePart(match.Value, false));
                continue;
            }

            if (canSpeak)
            {
                said = TransformSpeech(source, SanitizeMessageCapital(said));
                said = _language.ObfuscateMessageForSpeaker(source, said, language);
            }

            if (!canSpeak || said.Length == 0)
            {
                parts.Add(new CMUEmotePart(Loc.GetString("cmu-emote-speech-muffled"), true));
                continue;
            }

            spoken.Add(said);
            parts.Add(new CMUEmotePart(said, true));
        }

        if (last < action.Length)
            parts.Add(new CMUEmotePart(action[last..], false));

        var ent = Identity.Entity(source, EntityManager);
        var name = FormattedMessage.EscapeText(nameOverride ?? Name(ent));
        var unheard = Loc.GetString("cmu-emote-speech-unheard");

        // Same typeface as normal speech; the size is left to the emote so chat styles still apply.
        var speechFont = languagePrototype?.TypefaceId ??
                         GetSpeechVerb(source, spoken.Count > 0 ? string.Join(' ', spoken) : action).FontId;

        (string Plain, string Markup) Render(EntityUid? listener, bool cantHear)
        {
            var plain = new StringBuilder();
            var markup = new StringBuilder();
            foreach (var part in parts)
            {
                if (!part.Speech)
                {
                    plain.Append(part.Text);
                    markup.Append(CMUEscapeEmoteText(part.Text));
                    continue;
                }

                var said = part.Text;
                if (spoken.Count > 0 && listener != null && listener != source)
                {
                    said = cantHear
                        ? unheard
                        : _language.ObfuscateMessageForListener(listener.Value, said, language, source);
                }

                plain.Append('"').Append(said).Append('"');
                markup.Append($"[font=\"{speechFont}\"]\"")
                    .Append(CMUEscapeEmoteText(said))
                    .Append("\"[/font]");
            }

            return (FormattedMessage.RemoveMarkupPermissive(plain.ToString()), markup.ToString());
        }

        // The outer font tag keeps the client's chat font sizing on the whole emote instead of
        // only on the first quoted part.
        string Wrap(string message)
        {
            return "[font]" + Loc.GetString("chat-manager-entity-me-wrap-message",
                ("entityName", name),
                ("entity", ent),
                ("message", message)) + "[/font]";
        }

        foreach (var (session, data) in GetRecipients(source, VoiceRange))
        {
            if (!CanHearYautjaLocalSpeech(source, session, data))
                continue;

            var entRange = MessageRangeCheck(session, data, range);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;

            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            var cantHear = !data.Observer &&
                           (needsSpeech && HasComp<DeafComponent>(listener) ||
                            needsLos && !data.HasLOS);

            var (plain, markup) = Render(listener, cantHear);
            var ev = new ChatMessageOverrideInVoiceRangeEvent(
                session,
                ChatChannel.Emotes,
                source,
                plain,
                Wrap(markup),
                entRange == MessageRangeCheckResult.HideChat);
            RaiseLocalEvent(listener, ref ev);

            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                ev.Message,
                GetYautjaVisibleWrappedMessage(ev.WrappedMessage, source, session),
                data.BubbleSource ?? source,
                ev.EntHideChat,
                session.Channel);
        }

        var (speakerPlain, speakerMarkup) = Render(null, false);
        _replay.RecordServerMessage(new ChatMessage(
            ChatChannel.Emotes,
            speakerPlain,
            Wrap(speakerMarkup),
            GetNetEntity(source),
            null,
            MessageRangeHideChatForReplay(range)));

        if (spoken.Count > 0)
            RaiseLocalEvent(source, new EntitySpokeEvent(source, string.Join(' ', spoken), null, null, language), true);

        if (!hideLog)
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {ToPrettyString(source):user} in {language}: {action}");

        return true;
    }

    private static string CMUEscapeEmoteText(string text)
    {
        return FormattedMessage.EscapeText(FormattedMessage.RemoveMarkupPermissive(text));
    }
}
