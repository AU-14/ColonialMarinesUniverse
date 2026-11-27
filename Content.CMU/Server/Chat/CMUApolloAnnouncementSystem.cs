using System.Text;
using Content.Server._RMC14.Language.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.Language;
using Content.Server.CMU14.Platoon;
using Content.Server.CMU14.Roles;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Replays;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Chat;

public sealed partial class CMUApolloAnnouncementSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogs = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private CMUCharacterLanguageSystem _characters = default!;
    [Dependency] private RoundJobProfileSystem _jobs = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private PlatoonLanguageSystem _platoons = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IReplayRecordingManager _replay = default!;

    private const string Colony = "colony";

    public string? PresenceFaction(EntityUid crew, JobPrototype? job)
    {
        var side = _jobs.GetRoundSide(job);
        if (job != null && CMUCharacterLanguageSystem.IsColonyJob(job, side))
            return Colony;

        return side switch
        {
            RoundJobSide.Govfor => "govfor",
            RoundJobSide.Opfor => "opfor",
            _ => CompOrNull<MarineComponent>(crew)?.Faction,
        };
    }

    public ProtoId<LanguagePrototype> RadioLanguage(RadioChannelPrototype channel, string? crewFaction)
    {
        var faction = channel.Faction switch
        {
            "govfor" or "opfor" or "clf" => channel.Faction,
            _ when channel.ID is "Colony" or "colonyAlert" or "radioCMB" or "radioWEYU" or "ColonyHandheld" => Colony,
            _ => crewFaction ?? "govfor",
        };
        return FactionLanguage(faction);
    }

    private ProtoId<LanguagePrototype> FactionLanguage(string? faction)
    {
        faction = faction?.Trim().ToLowerInvariant();
        if (faction is Colony or "clf")
            return _characters.GetColonyLanguages()?.DefaultLanguage ?? SharedLanguageSystem.CommonLanguage;

        if (faction is "marine" or null)
            faction = "govfor";

        var platoon = _platoons.GetFactionPlatoon(faction);
        return platoon == null
            ? SharedLanguageSystem.CommonLanguage
            : PlatoonLanguageSystem.GetDefaultLanguage(platoon) ?? SharedLanguageSystem.CommonLanguage;
    }

    // An all-sides broadcast is issued by each faction's own APOLLO.
    public ProtoId<LanguagePrototype> BroadcastLanguage(EntityUid? listener, string? faction)
    {
        if (string.IsNullOrWhiteSpace(faction))
            faction = TryComp<MarineComponent>(listener, out var marine) ? marine.Faction : Colony;

        return FactionLanguage(faction);
    }

    // scramble: false for game notices with no in-world speaker, which everyone must be able to read (BUG-1576)
    public void SendChat(Filter filter, string message, Func<string, string> wrap, string? faction,
        EntityUid? source = null, Color? color = null, bool scramble = true)
    {
        var clear = wrap(message);
        foreach (var session in filter.Recipients)
        {
            var language = BroadcastLanguage(session.AttachedEntity, faction);
            var heard = scramble && session.AttachedEntity is { } listener
                ? _language.ObfuscateMessageForListener(listener, message, language, source ?? EntityUid.Invalid)
                : message;
            var prototype = _prototypes.Index(language);
            _chat.ChatMessageToOne(ChatChannel.Radio, heard, heard == message ? clear : wrap(heard),
                source ?? EntityUid.Invalid, false, session.Channel, colorOverride: color,
                languageIcon: prototype.HideLanguageName ? null : prototype.DisplayedLanguageIcon);
        }

        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Radio, message, clear, GetNetEntity(source ?? EntityUid.Invalid), null, colorOverride: color));
    }

    public void AnnounceColony(string message, string sender, SoundSpecifier? sound, EntityUid? source = null, Color? color = null,
        bool scramble = true)
    {
        var filter = ColonyAnnouncements.Recipients();
        SendChat(filter, message, heard => Loc.GetString("chat-manager-sender-announcement-wrap-message",
            ("sender", sender), ("message", FormattedMessage.EscapeText(heard))), Colony, source, color, scramble);
        if (sound != null)
            _audio.PlayGlobal(sound, filter, true, AudioParams.Default.WithVolume(-2f));
        _adminLogs.Add(LogType.Chat, LogImpact.Low, $"Station Announcement from {sender}: {message}");
    }

    public string[] OverlayLines(EntityUid? listener, string[] lines, EntityUid? source)
    {
        if (listener is not { } entity)
            return lines;

        var language = BroadcastLanguage(entity, null);
        if (_language.CanUnderstand(entity, language))
            return lines;

        // The overlay supports rich text. Pass only text nodes through speech comprehension.
        var heard = new string[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var parsed = FormattedMessage.FromMarkupPermissive(lines[i]);
            var result = new StringBuilder();
            foreach (var node in parsed.Nodes)
            {
                if (node.Name == null && node.Value.StringValue is { } text)
                    result.Append(FormattedMessage.EscapeText(_language.ObfuscateMessageForListener(entity, text,
                        language, source ?? EntityUid.Invalid)));
                else
                    result.Append(node);
            }
            heard[i] = result.ToString();
        }
        return heard;
    }
}
