using System.Net;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.CMU.Round;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        private static readonly ResPath RoundStatusWebhookMessageIdsPath =
            new("/discord/round-status-webhook-message-ids.json");

        private readonly List<RoundStatusRecentGamemode> _recentRoundStatusGamemodes = new();

        private string GetDiscordMapName()
        {
            var mapName = GetPlanetMapName();
            return mapName == Loc.GetString("game-ticker-no-map-selected-plain")
                ? Loc.GetString("discord-round-notifications-unknown-map")
                : mapName;
        }

        private async void SendRoundEndDiscordMessage()
        {
            try
            {
                await SendRoundStatusDiscordMessage(RoundStatusWebhookKind.Ended, false);
                await SendRoundStatusRolePingMessage(RoundStatusPingMessageKind.RoundEnd, GetRoundEndRoleIds());
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord round end message:\n{e}");
            }
        }

        private Task SendRoundStatusDiscordMessage(RoundStatusWebhookKind kind, bool pingRoles)
        {
            var roles = pingRoles
                ? GetRoundStatusRoleIds(true)
                : Array.Empty<string>();

            return SendRoundStatusDiscordMessage(kind, roles);
        }

        private async Task SendRoundStatusDiscordMessage(RoundStatusWebhookKind kind, IEnumerable<string> roles)
        {
            if (_webhookIdentifier == null)
                return;

            var status = GetRoundStatusWebhookData(GetRoundStatusDuration(kind));
            var payload = RoundStatusWebhook.CreatePayload(kind, status, roles, DiscordRoundStatusColors);

            if (_roundStatusWebhookMessageId == 0)
            {
                await CreateRoundStatusWebhookMessage(payload);
                ScheduleNextRoundStatusWebhookUpdate();
                return;
            }

            var response = await _discord.EditMessage(_webhookIdentifier.Value, _roundStatusWebhookMessageId, payload);
            if (response.IsSuccessStatusCode)
            {
                SaveRoundStatusWebhookMessageIds();
                ScheduleNextRoundStatusWebhookUpdate();
                return;
            }

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                ScheduleNextRoundStatusWebhookUpdate();
                return;
            }

            _roundStatusWebhookMessageId = 0;
            SaveRoundStatusWebhookMessageIds();
            await CreateRoundStatusWebhookMessage(payload);
            ScheduleNextRoundStatusWebhookUpdate();
        }

        private async Task CreateRoundStatusWebhookMessage(WebhookPayload payload)
        {
            var response = await _discord.CreateMessage(_webhookIdentifier!.Value, payload);
            var content = await response.Content.ReadAsStringAsync();

            if (RoundStatusWebhook.TryGetMessageId(content, out var messageId))
            {
                _roundStatusWebhookMessageId = messageId;
                SaveRoundStatusWebhookMessageIds();
            }
        }

        private void LoadRoundStatusWebhookMessageIds()
        {
            try
            {
                if (!_resourceManager.UserData.TryReadAllText(RoundStatusWebhookMessageIdsPath, out var json))
                    return;

                if (!RoundStatusWebhook.TryDeserializeMessageIds(json, out var ids))
                {
                    Log.Warning("Failed to parse persisted Discord round status webhook message IDs.");
                    return;
                }

                _roundStatusWebhookMessageId = ids.StatusMessageId;
                _roundStatusRoundEndPingMessageId = ids.RoundEndPingMessageId;
                _roundStatusGamemodeVotePingMessageId = ids.GamemodeVotePingMessageId;
            }
            catch (Exception e)
            {
                Log.Warning($"Error while loading Discord round status webhook message IDs:\n{e}");
            }
        }

        private void SaveRoundStatusWebhookMessageIds()
        {
            try
            {
                var ids = new RoundStatusWebhookMessageIds(
                    _roundStatusWebhookMessageId,
                    _roundStatusRoundEndPingMessageId,
                    _roundStatusGamemodeVotePingMessageId);

                _resourceManager.UserData.CreateDir(RoundStatusWebhookMessageIdsPath.Directory);
                _resourceManager.UserData.WriteAllText(
                    RoundStatusWebhookMessageIdsPath,
                    RoundStatusWebhook.SerializeMessageIds(ids));
            }
            catch (Exception e)
            {
                Log.Warning($"Error while saving Discord round status webhook message IDs:\n{e}");
            }
        }

        private RoundStatusWebhookData GetRoundStatusWebhookData(TimeSpan? duration)
        {
            var gamemode = CurrentPreset != null
                ? Loc.GetString(CurrentPreset.ModeTitle)
                : Preset != null
                    ? Loc.GetString(Preset.ModeTitle)
                    : string.Empty;
            var govfor = _cmuRoundDirector.TryGetLegacyForceProjection(
                RoundSide.Govfor,
                out var govforForce)
                ? govforForce.Name
                : string.Empty;

            return new RoundStatusWebhookData(
                RoundId,
                _playerManager.PlayerCount,
                GetDiscordMapName(),
                govfor,
                gamemode,
                _recentRoundStatusGamemodes.ToArray(),
                duration);
        }

        private void RememberRoundStatusGamemode(int roundId, string gamemode, TimeSpan duration)
        {
            if (roundId <= 0)
                return;

            if (string.IsNullOrWhiteSpace(gamemode))
                gamemode = Loc.GetString("ui-escape-status-unknown");

            var existingIndex = _recentRoundStatusGamemodes.FindIndex(round => round.RoundId == roundId);
            if (existingIndex >= 0)
                _recentRoundStatusGamemodes.RemoveAt(existingIndex);

            _recentRoundStatusGamemodes.Insert(0, new RoundStatusRecentGamemode(roundId, gamemode, duration));

            if (_recentRoundStatusGamemodes.Count > 3)
                _recentRoundStatusGamemodes.RemoveRange(3, _recentRoundStatusGamemodes.Count - 3);
        }

        private TimeSpan? GetRoundStatusDuration(RoundStatusWebhookKind kind)
        {
            if (kind == RoundStatusWebhookKind.Ended || RunLevel != GameRunLevel.PreRoundLobby)
                return RoundDuration();

            return null;
        }

        private void ScheduleNextRoundStatusWebhookUpdate()
        {
            _nextRoundStatusWebhookUpdate = _gameTiming.CurTime + DiscordRoundStatusUpdateInterval;
        }

        internal static bool TryGetPeriodicRoundStatusWebhookKind(GameRunLevel runLevel, out RoundStatusWebhookKind kind)
        {
            switch (runLevel)
            {
                case GameRunLevel.PreRoundLobby:
                    kind = RoundStatusWebhookKind.Lobby;
                    return true;
                case GameRunLevel.InRound:
                    kind = RoundStatusWebhookKind.Running;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private void TrySendInitialRoundStatusDiscordMessage()
        {
            if (!_postInitialized || DummyTicker || _webhookIdentifier == null || _roundStatusWebhookWakeSent)
                return;

            SendRoundStartingDiscordMessage();
        }

        private async void SendRoundStartingDiscordMessage()
        {
            if (_webhookIdentifier == null)
                return;

            try
            {
                _roundStatusWebhookWakeSent = true;
                await DeleteRoundStatusPingMessages();
                await SendRoundStatusDiscordMessage(RoundStatusWebhookKind.Lobby, false);
            }
            catch (Exception e)
            {
                _roundStatusWebhookWakeSent = false;
                Log.Error($"Error while sending discord round starting status message:\n{e}");
            }
        }

        private void SendServerShutdownDiscordMessage()
        {
            if (_webhookIdentifier == null || DummyTicker)
                return;

            try
            {
                var sendTask = SendRoundStatusDiscordMessage(RoundStatusWebhookKind.Shutdown, false);
                var waitTask = Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(5)));
                _taskManager.BlockWaitOnTask(waitTask);

                if (!sendTask.IsCompleted)
                {
                    Log.Warning("Timed out while sending discord shutdown status message.");
                    return;
                }

                _taskManager.BlockWaitOnTask(sendTask);
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord shutdown status message:\n{e}");
            }
        }

        private async void UpdateRoundStatusDiscordMessage(RoundStatusWebhookKind kind)
        {
            if (_roundStatusWebhookUpdatePending)
                return;

            try
            {
                _roundStatusWebhookUpdatePending = true;
                await SendRoundStatusDiscordMessage(kind, false);
            }
            catch (Exception e)
            {
                Log.Error($"Error while updating discord round status message:\n{e}");
                ScheduleNextRoundStatusWebhookUpdate();
            }
            finally
            {
                _roundStatusWebhookUpdatePending = false;
            }
        }

        internal async void SendGamemodeVoteWinnerDiscordPing(string? presetId)
        {
            if (_webhookIdentifier == null || DummyTicker)
                return;

            var role = RoundStatusWebhook.GetGamemodeRole(
                presetId,
                DiscordRoundStatusDistressSignalRole,
                DiscordRoundStatusColonyFallRole,
                DiscordRoundStatusInsurgencyRole);

            if (role == null)
                return;

            try
            {
                await SendRoundStatusRolePingMessage(RoundStatusPingMessageKind.GamemodeVote, new[] { role });
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord gamemode vote ping:\n{e}");
            }
        }

        private async Task SendRoundStatusRolePingMessage(RoundStatusPingMessageKind kind, IEnumerable<string> roles)
        {
            if (_webhookIdentifier == null)
                return;

            var message = kind == RoundStatusPingMessageKind.GamemodeVote
                ? Loc.GetString("discord-round-notifications-gamemode-voted")
                : null;
            var payload = RoundStatusWebhook.CreateRolePingPayload(roles, message);
            if (string.IsNullOrWhiteSpace(payload.Content))
                return;

            var response = await _discord.CreateMessage(_webhookIdentifier.Value, payload);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode || !RoundStatusWebhook.TryGetMessageId(content, out var messageId))
                return;

            var previousMessageId = GetRoundStatusPingMessageId(kind);
            SetRoundStatusPingMessageId(kind, messageId);
            SaveRoundStatusWebhookMessageIds();

            if (RoundStatusWebhook.TryGetMessageIdToDelete(previousMessageId, messageId, out var deleteMessageId))
                await _discord.DeleteMessage(_webhookIdentifier.Value, deleteMessageId);
        }

        private async Task DeleteRoundStatusPingMessages()
        {
            await DeleteRoundStatusPingMessage(RoundStatusPingMessageKind.RoundEnd);
            await DeleteRoundStatusPingMessage(RoundStatusPingMessageKind.GamemodeVote);
        }

        private async Task DeleteRoundStatusPingMessage(RoundStatusPingMessageKind kind)
        {
            if (_webhookIdentifier == null)
                return;

            var messageId = GetRoundStatusPingMessageId(kind);
            if (messageId == 0)
                return;

            var response = await _discord.DeleteMessage(_webhookIdentifier.Value, messageId);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                return;

            SetRoundStatusPingMessageId(kind, 0);
            SaveRoundStatusWebhookMessageIds();
        }

        private ulong GetRoundStatusPingMessageId(RoundStatusPingMessageKind kind)
        {
            return kind switch
            {
                RoundStatusPingMessageKind.RoundEnd => _roundStatusRoundEndPingMessageId,
                RoundStatusPingMessageKind.GamemodeVote => _roundStatusGamemodeVotePingMessageId,
                _ => 0,
            };
        }

        private void SetRoundStatusPingMessageId(RoundStatusPingMessageKind kind, ulong messageId)
        {
            switch (kind)
            {
                case RoundStatusPingMessageKind.RoundEnd:
                    _roundStatusRoundEndPingMessageId = messageId;
                    break;
                case RoundStatusPingMessageKind.GamemodeVote:
                    _roundStatusGamemodeVotePingMessageId = messageId;
                    break;
            }
        }

        private IEnumerable<string> GetRoundStatusRoleIds(bool includeRoundEndRole)
        {
            return RoundStatusWebhook.GetRoundStatusRoleIds(
                includeRoundEndRole,
                CurrentPreset?.ID ?? Preset?.ID,
                DiscordRoundEndRole,
                DiscordRoundStatusDistressSignalRole,
                DiscordRoundStatusColonyFallRole,
                DiscordRoundStatusInsurgencyRole);
        }

        private IEnumerable<string> GetRoundEndRoleIds()
        {
            if (DiscordRoundEndRole is { } roundEndRole)
                yield return roundEndRole;
        }

        private async void SendRoundStartedDiscordMessage()
        {
            try
            {
                await SendRoundStatusDiscordMessage(RoundStatusWebhookKind.Running, false);
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord round start message:\n{e}");
            }
        }
    }

    internal enum RoundStatusPingMessageKind
    {
        RoundEnd,
        GamemodeVote,
    }
}
