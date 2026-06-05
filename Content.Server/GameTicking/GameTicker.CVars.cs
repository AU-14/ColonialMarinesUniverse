using Content.Server.Discord;
using Content.Shared._RMC14.CCVar;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [ViewVariables]
        public bool LobbyEnabled { get; private set; }

        [ViewVariables]
        public bool DummyTicker { get; private set; } = false;

        [ViewVariables]
        public TimeSpan LobbyDuration { get; private set; } = TimeSpan.Zero;

        [ViewVariables]
        public int LobbyMinimumPlayers { get; private set; } = 1;

        [ViewVariables]
        public bool DisallowLateJoin { get; private set; } = false;

        [ViewVariables]
        public string? ServerName { get; private set; }

        [ViewVariables]
        private string? DiscordRoundEndRole { get; set; }

        [ViewVariables]
        private string? DiscordRoundStatusDistressSignalRole { get; set; }

        [ViewVariables]
        private string? DiscordRoundStatusColonyFallRole { get; set; }

        [ViewVariables]
        private string? DiscordRoundStatusInsurgencyRole { get; set; }

        private WebhookIdentifier? _webhookIdentifier;

        private ulong _roundStatusWebhookMessageId;

        private TimeSpan DiscordRoundStatusUpdateInterval { get; set; } = TimeSpan.FromSeconds(60);

        private RoundStatusWebhookColors DiscordRoundStatusColors { get; set; } = RoundStatusWebhook.DefaultColors;

        private TimeSpan _nextRoundStatusWebhookUpdate;

        private bool _roundStatusWebhookUpdatePending;

        private bool _roundStatusWebhookWakeSent;

        [ViewVariables]
        private string? RoundEndSoundCollection { get; set; }

#if EXCEPTION_TOLERANCE
        [ViewVariables]
        public int RoundStartFailShutdownCount { get; private set; } = 0;
#endif

        private void InitializeCVars()
        {
            Subs.CVar(_cfg, CCVars.GameLobbyEnabled, value =>
            {
                LobbyEnabled = value;
                foreach (var (userId, status) in _playerGameStatuses)
                {
                    if (status == PlayerGameStatus.JoinedGame)
                        continue;
                    _playerGameStatuses[userId] =
                        LobbyEnabled ? PlayerGameStatus.NotReadyToPlay : PlayerGameStatus.ReadyToPlay;
                }
            }, true);
            Subs.CVar(_cfg, CCVars.GameDummyTicker, value => DummyTicker = value, true);
            Subs.CVar(_cfg, CCVars.GameLobbyDuration, value => LobbyDuration = TimeSpan.FromSeconds(value), true);
            Subs.CVar(_cfg, RMCCVars.RMCLobbyMinimumPlayers, value => LobbyMinimumPlayers = value, true);
            Subs.CVar(_cfg, CCVars.GameDisallowLateJoins,
                value => { DisallowLateJoin = value; UpdateLateJoinStatus(); }, true);
            Subs.CVar(_cfg, CCVars.AdminLogsServerName, value =>
            {
                // TODO why tf is the server name on admin logs
                ServerName = value;
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundUpdateWebhook, value =>
            {
                _webhookIdentifier = null;
                _roundStatusWebhookMessageId = 0;
                _roundStatusWebhookWakeSent = false;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _discord.GetWebhook(value, data =>
                    {
                        _webhookIdentifier = data.ToIdentifier();
                        TrySendInitialRoundStatusDiscordMessage();
                    });
                }
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundEndRoleWebhook, value =>
            {
                DiscordRoundEndRole = NullIfEmpty(value);
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusDistressSignalRole, value =>
            {
                DiscordRoundStatusDistressSignalRole = NullIfEmpty(value);
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusColonyFallRole, value =>
            {
                DiscordRoundStatusColonyFallRole = NullIfEmpty(value);
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusInsurgencyRole, value =>
            {
                DiscordRoundStatusInsurgencyRole = NullIfEmpty(value);
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusUpdateInterval, value =>
            {
                DiscordRoundStatusUpdateInterval = TimeSpan.FromSeconds(value < 0 ? 0 : value);
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusStartingColor, value =>
            {
                DiscordRoundStatusColors = DiscordRoundStatusColors with
                {
                    Starting = RoundStatusWebhook.ParseColor(value, RoundStatusWebhook.DefaultColors.Starting),
                };
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusRunningColor, value =>
            {
                DiscordRoundStatusColors = DiscordRoundStatusColors with
                {
                    Running = RoundStatusWebhook.ParseColor(value, RoundStatusWebhook.DefaultColors.Running),
                };
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusEndedColor, value =>
            {
                DiscordRoundStatusColors = DiscordRoundStatusColors with
                {
                    Ended = RoundStatusWebhook.ParseColor(value, RoundStatusWebhook.DefaultColors.Ended),
                };
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundStatusShutdownColor, value =>
            {
                DiscordRoundStatusColors = DiscordRoundStatusColors with
                {
                    Shutdown = RoundStatusWebhook.ParseColor(value, RoundStatusWebhook.DefaultColors.Shutdown),
                };
            }, true);
            Subs.CVar(_cfg, CCVars.RoundEndSoundCollection, value => RoundEndSoundCollection = value, true);
#if EXCEPTION_TOLERANCE
            Subs.CVar(_cfg, CCVars.RoundStartFailShutdownCount, value => RoundStartFailShutdownCount = value, true);
#endif
        }

        private static string? NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }
    }
}
