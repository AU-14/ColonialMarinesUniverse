using System.Globalization;
using Content.Server._RMC14.Admin;
using Content.Server._RMC14.Chat.Chat;
using Content.Server._RMC14.Emote;
using Content.Server._RMC14.Language.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._AU14.Marines.Orders;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Radio;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;

namespace Content.Server.Chat.Systems;

// TODO refactor whatever active warzone this class and chatmanager have become
/// <summary>
/// ChatSystem is responsible for in-simulation chat handling, such as whispering, speaking, emoting, etc.
/// ChatSystem depends on ChatManager to actually send the messages.
/// </summary>
public sealed partial class ChatSystem : SharedChatSystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IChatSanitizationManager _sanitizer = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ReplacementAccentSystem _wordreplacement = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private EntityQuery<GhostHearingComponent> _ghostHearingQuery = default!;
    [Dependency] private CMChatSystem _cmChat = default!;
    [Dependency] private RMCEmoteSystem _rmcEmote = default!;
    [Dependency] private INetConfigurationManager _netConfigManager = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private RMCChatBansManager _rmcChatBans = default!;

    private bool _loocEnabled = true;
    private bool _deadLoocEnabled;
    private bool _critLoocEnabled;
    private readonly bool _adminLoocEnabled = true;
    private bool _deadChatEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configurationManager, CCVars.LoocEnabled, OnLoocEnabledChanged, true);
        Subs.CVar(_configurationManager, CCVars.DeadLoocEnabled, OnDeadLoocEnabledChanged, true);
        Subs.CVar(_configurationManager, CCVars.CritLoocEnabled, OnCritLoocEnabledChanged, true);
        Subs.CVar(_configurationManager, CCVars.DeadChatEnabled, OnDeadChatEnabledChanged, true);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameChange);
    }

    private void OnLoocEnabledChanged(bool val)
    {
        if (_loocEnabled == val)
            return;

        _loocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-looc-chat-enabled-message" : "chat-manager-looc-chat-disabled-message"));
    }

    private void OnDeadLoocEnabledChanged(bool val)
    {
        if (_deadLoocEnabled == val)
            return;

        _deadLoocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-dead-looc-chat-enabled-message" : "chat-manager-dead-looc-chat-disabled-message"));
    }

    private void OnCritLoocEnabledChanged(bool val)
    {
        if (_critLoocEnabled == val)
            return;

        _critLoocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-crit-looc-chat-enabled-message" : "chat-manager-crit-looc-chat-disabled-message"));
    }

    private void OnDeadChatEnabledChanged(bool val)
    {
        if (_deadChatEnabled == val)
            return;

        _deadChatEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-dead-chat-enabled-message" : "chat-manager-dead-chat-disabled-message"));
    }

    private void OnGameChange(GameRunLevelChangedEvent ev)
    {
        switch (ev.New)
        {
            case GameRunLevel.InRound:
                if (!_configurationManager.GetCVar(CCVars.OocEnableDuringRound))
                    _configurationManager.SetCVar(CCVars.OocEnabled, false);
                break;
            case GameRunLevel.PostRound:
            case GameRunLevel.PreRoundLobby:
                if (!_configurationManager.GetCVar(CCVars.OocEnableDuringRound))
                    _configurationManager.SetCVar(CCVars.OocEnabled, true);
                break;
        }
    }

    /// <inheritdoc />
    public override void TrySendInGameICMessage(
        EntityUid source,
        string message,
        InGameICChatType desiredType,
        bool hideChat,
        bool hideLog = false,
        IConsoleShell? shell = null,
        ICommonSession? player = null,
        string? nameOverride = null,
        bool checkRadioPrefix = true,
        bool ignoreActionBlocker = false)
    {
        TrySendInGameICMessage(
            source,
            message,
            desiredType,
            hideChat ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal,
            hideLog,
            shell,
            player,
            nameOverride,
            checkRadioPrefix,
            ignoreActionBlocker);
    }

    /// <inheritdoc />
    public override void TrySendInGameICMessage(
        EntityUid source,
        string message,
        InGameICChatType desiredType,
        ChatTransmitRange range,
        bool hideLog = false,
        IConsoleShell? shell = null,
        ICommonSession? player = null,
        string? nameOverride = null,
        bool checkRadioPrefix = true,
        bool ignoreActionBlocker = false)
    {
        if (HasComp<GhostComponent>(source) && !HasComp<ImaginaryFriendComponent>(source))
        {
            // Ghosts can only send dead chat messages, so forward it to in-game OOC.
            TrySendInGameOOCMessage(
                source,
                message,
                InGameOOCChatType.Dead,
                range == ChatTransmitRange.HideChat,
                shell,
                player);
            return;
        }

        if (player != null && _chatManager.HandleRateLimit(player) != RateLimitStatus.Allowed)
            return;

        if (player?.AttachedEntity is { Valid: true } entity && source != entity)
            return;

        if (!CanSendInGame(message, shell, player))
            return;

        ignoreActionBlocker = CheckIgnoreSpeechBlocker(source, ignoreActionBlocker);

        if (player != null)
            _chatManager.EnsurePlayer(player.UserId).AddEntity(GetNetEntity(source));

        var currentLanguage = GetCurrentLanguageForSpeech(source);

        if (desiredType == InGameICChatType.Speak && message.StartsWith(LocalPrefix))
        {
            // Prevent radios and remove the prefix.
            checkRadioPrefix = false;
            message = message[1..];
        }

        if (desiredType == InGameICChatType.Speak && HasComp<AU14SilenceOrderComponent>(source))
        {
            desiredType = InGameICChatType.Whisper;
            checkRadioPrefix = false;
        }

        var shouldCapitalize = desiredType != InGameICChatType.Emote;
        var shouldPunctuate = _configurationManager.GetCVar(CCVars.ChatPunctuation) ||
                              player != null &&
                              _netConfigManager.GetClientCVar(player.Channel, RMCCVars.RMCAutoPunctuate);
        // Capitalizing the word I only happens in English, so check the current culture here.
        var shouldCapitalizeTheWordI =
            !CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Parent.Name == "en" ||
            CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Name == "en";

        var isRadioMessage = checkRadioPrefix &&
                             TryProcessRadioMessage(source, message, out var radioText, out _) &&
                             !string.IsNullOrWhiteSpace(radioText);

        message = SanitizeInGameICMessage(
            source,
            message,
            out var emoteStr,
            shouldCapitalize,
            shouldPunctuate,
            shouldCapitalizeTheWordI,
            skipEmoteShorthands: isRadioMessage);

        if (player != null && emoteStr != message && emoteStr != null)
            SendEntityEmote(source, emoteStr, range, nameOverride, ignoreActionBlocker: ignoreActionBlocker);

        if (string.IsNullOrEmpty(message))
            return;

        if (checkRadioPrefix)
        {
            var messages = _cmChat.TryMultiBroadcast(source, message);
            if (messages != null)
            {
                var channelsSent = new HashSet<ProtoId<RadioChannelPrototype>>();
                foreach (var radioMessage in messages)
                {
                    if (!TryProcessRadioMessage(source, radioMessage, out var broadcastMessage, out var broadcastChannel))
                        continue;

                    if (broadcastChannel != null && channelsSent.Contains(broadcastChannel.ID))
                        continue;

                    SendEntityWhisperWithLanguage(
                        source,
                        broadcastMessage,
                        range,
                        broadcastChannel,
                        nameOverride,
                        hideLog,
                        ignoreActionBlocker,
                        currentLanguage);

                    if (broadcastChannel != null)
                        channelsSent.Add(broadcastChannel.ID);
                }

                return;
            }

            if (TryProcessRadioMessage(source, message, out var modifiedMessage, out var channel))
            {
                SendEntityWhisperWithLanguage(
                    source,
                    modifiedMessage,
                    range,
                    channel,
                    nameOverride,
                    hideLog,
                    ignoreActionBlocker,
                    currentLanguage);
                return;
            }
        }

        switch (desiredType)
        {
            case InGameICChatType.Speak:
                SendEntitySpeakWithLanguage(
                    source,
                    message,
                    range,
                    nameOverride,
                    hideLog,
                    ignoreActionBlocker,
                    currentLanguage);
                break;
            case InGameICChatType.Whisper:
                SendEntityWhisperWithLanguage(
                    source,
                    message,
                    range,
                    null,
                    nameOverride,
                    hideLog,
                    ignoreActionBlocker,
                    currentLanguage);
                break;
            case InGameICChatType.Emote:
                SendEntityEmote(
                    source,
                    message,
                    range,
                    nameOverride,
                    hideLog: hideLog,
                    ignoreActionBlocker: ignoreActionBlocker);
                break;
        }
    }

    /// <inheritdoc />
    public override void TrySendInGameOOCMessage(
        EntityUid source,
        string message,
        InGameOOCChatType type,
        bool hideChat,
        IConsoleShell? shell = null,
        ICommonSession? player = null)
    {
        if (!CanSendInGame(message, shell, player))
            return;

        if (player != null && _chatManager.HandleRateLimit(player) != RateLimitStatus.Allowed)
            return;

        // Non-players can send IC messages, but in-game OOC always requires an attached player.
        if (player?.AttachedEntity is not { Valid: true } entity || source != entity)
            return;

        message = SanitizeInGameOOCMessage(message);

        var sendType = type;
        // If dead-player LOOC is disabled, redirect it to dead chat unless a moderator is speaking.
        if (!((_adminManager.IsAdmin(player) && _adminManager.HasAdminFlag(player, AdminFlags.Moderator)) ||
              _deadLoocEnabled ||
              (!HasComp<GhostComponent>(source) && !_mobStateSystem.IsDead(source))))
        {
            sendType = InGameOOCChatType.Dead;
        }

        if (!_critLoocEnabled && _mobStateSystem.IsCritical(source))
            return;

        var ev = new InGameOocMessageAttemptEvent(player, sendType);
        RaiseLocalEvent(source, ref ev, true);
        if (ev.Cancelled)
            return;

        switch (sendType)
        {
            case InGameOOCChatType.Dead:
                SendDeadChat(source, player, message, hideChat);
                break;
            case InGameOOCChatType.Looc:
                SendLOOC(source, player, message, hideChat);
                break;
        }
    }
}

/// <summary>
/// Raised before chat messages are sent to clients so systems can add otherwise out-of-view recipients.
/// </summary>
public record ExpandICChatRecipientsEvent(
    EntityUid Source,
    float VoiceRange,
    Dictionary<ICommonSession, ChatSystem.ICChatRecipientData> Recipients);
