using System.Collections.Generic;
using System.Linq;
using Content.Server.Maps;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Server.AU14.Round;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [Dependency] private AuRoundSystem _auRoundSystem = default!;
        [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;

        [ViewVariables]
        private readonly Dictionary<NetUserId, PlayerGameStatus> _playerGameStatuses = new();

        [ViewVariables]
        private TimeSpan _roundStartTime;

        /// <summary>
        /// How long before RoundStartTime do we load maps.
        /// </summary>
        [ViewVariables]
        public TimeSpan RoundPreloadTime { get; } = TimeSpan.FromSeconds(15);

        [ViewVariables]
        private TimeSpan _pauseTime;

        [ViewVariables]
        public new bool Paused { get; set; }

        [ViewVariables]
        private bool _roundStartCountdownHasNotStartedYetDueToNoPlayers;

        /// <summary>
        /// The game status of a players user Id. May contain disconnected players
        /// </summary>
        public IReadOnlyDictionary<NetUserId, PlayerGameStatus> PlayerGameStatuses => _playerGameStatuses;

        public void UpdateInfoText()
        {
            var filter = Filter.Empty().AddPlayers(_playerManager.NetworkedSessions);
            RaiseNetworkEvent(GetInfoMsg(), filter);
            RaiseNetworkEvent(GetRoundStatusMsg(), filter);
        }

        private string GetPlanetMapName()
        {
            var selectedPlanet = _auRoundSystem.GetSelectedPlanet();
            if (!string.IsNullOrWhiteSpace(selectedPlanet?.VoteName))
                return selectedPlanet.VoteName;

            if (!string.IsNullOrWhiteSpace(selectedPlanet?.MapId) &&
                _prototypeManager.TryIndex<GameMapPrototype>(selectedPlanet.MapId, out var selectedPlanetMap))
            {
                return selectedPlanetMap.MapName;
            }

            if (!string.IsNullOrWhiteSpace(_distressSignal.SelectedPlanetMapName))
                return _distressSignal.SelectedPlanetMapName;

            return Loc.GetString("game-ticker-no-map-selected-plain");
        }

        private string GetShipMapName()
        {
            var shipNames = new List<string>();
            AddShipMapName(_auRoundSystem.GetSelectedGovforShip(), shipNames);
            AddShipMapName(_auRoundSystem.GetSelectedOpforShip(), shipNames);

            if (shipNames.Count > 0)
                return string.Join(" / ", shipNames.Distinct());

            return Loc.GetString("ui-escape-status-no-ship");
        }

        private void AddShipMapName(string? mapId, List<string> shipNames)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return;

            if (_prototypeManager.TryIndex<GameMapPrototype>(mapId, out var shipMap))
            {
                shipNames.Add(shipMap.MapName);
                return;
            }

            shipNames.Add(mapId);
        }

        private string LocalizeOrRaw(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return Loc.TryGetString(text, out var localized)
                ? localized
                : text;
        }

        private string GetInfoText()
        {
            var preset = CurrentPreset ?? Preset;
            if (preset == null)
            {
                return string.Empty;
            }

            var playerCount = $"{_playerManager.PlayerCount}";
            var readyCount = _playerGameStatuses.Values.Count(x => x == PlayerGameStatus.ReadyToPlay);
            var planetName = GetPlanetMapName();

            var govforShip = _auRoundSystem.GetSelectedGovforShip();
            var opforShip = _auRoundSystem.GetSelectedOpforShip();
            var govforShipDisplay = !string.IsNullOrWhiteSpace(govforShip) ? govforShip : "None";
            var opforShipDisplay = !string.IsNullOrWhiteSpace(opforShip) ? opforShip : "None";

            var gmTitle = LocalizeOrRaw(preset.ModeTitle);
            var desc = LocalizeOrRaw(preset.Description);
            var govforPlatoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon?.Name;
            var opforPlatoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon?.Name;
            var govforPlatoonDisplay = !string.IsNullOrWhiteSpace(govforPlatoon) ? govforPlatoon : "None";
            var opforPlatoonDisplay = !string.IsNullOrWhiteSpace(opforPlatoon) ? opforPlatoon : "None";
            return Loc.GetString(
                RunLevel == GameRunLevel.PreRoundLobby
                    ? "game-ticker-get-info-preround-text"
                    : "game-ticker-get-info-text",
                ("roundId", RoundId),
                ("playerCount", playerCount),
                ("readyCount", readyCount),
                ("planetName", planetName),
                ("govforShip", govforShipDisplay),
                ("opforShip", opforShipDisplay),
                ("govforPlatoon", govforPlatoonDisplay),
                ("opforPlatoon", opforPlatoonDisplay),
                ("mapName", GetPlanetMapName()),
                ("gmTitle", gmTitle),
                ("desc", desc));
        }

        private TickerConnectionStatusEvent GetConnectionStatusMsg()
        {
            return new TickerConnectionStatusEvent(RoundStartTimeSpan);
        }

        private TickerLobbyStatusEvent GetStatusMsg(ICommonSession session)
        {
            _playerGameStatuses.TryGetValue(session.UserId, out var status);
            return new TickerLobbyStatusEvent(RunLevel != GameRunLevel.PreRoundLobby, LobbyBackground, status == PlayerGameStatus.ReadyToPlay, _roundStartTime, RoundPreloadTime, RoundStartTimeSpan, Paused);
        }

        private void SendStatusToAll()
        {
            foreach (var player in _playerManager.Sessions)
            {
                RaiseNetworkEvent(GetStatusMsg(player), player.Channel);
            }
        }

        private TickerLobbyInfoEvent GetInfoMsg()
        {
            return new (GetInfoText());
        }

        private TickerRoundStatusEvent GetRoundStatusMsg()
        {
            var preset = CurrentPreset ?? Preset;
            var gamemodeTitle = preset != null
                ? LocalizeOrRaw(preset.ModeTitle)
                : Loc.GetString("ui-escape-status-unknown");

            return new TickerRoundStatusEvent(
                GetPlanetMapName(),
                GetShipMapName(),
                RoundId,
                _playerManager.PlayerCount,
                gamemodeTitle,
                RoundStartTimeSpan,
                RealRoundDuration(),
                RunLevel != GameRunLevel.PreRoundLobby);
        }

        private TimeSpan RealRoundDuration()
        {
            if (RunLevel == GameRunLevel.PreRoundLobby || _roundStartDateTime == default)
                return TimeSpan.Zero;

            var elapsed = DateTime.UtcNow - _roundStartDateTime;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }

        private void UpdateLateJoinStatus()
        {
            RaiseNetworkEvent(new TickerLateJoinStatusEvent(DisallowLateJoin));
        }

        public bool PauseStart(bool pause = true)
        {
            if (Paused == pause)
            {
                return false;
            }

            Paused = pause;

            if (pause)
            {
                _pauseTime = _gameTiming.CurTime;
            }
            else if (_pauseTime != default)
            {
                _roundStartTime += _gameTiming.CurTime - _pauseTime;
            }

            RaiseNetworkEvent(new TickerLobbyCountdownEvent(_roundStartTime, Paused));

            _chatManager.DispatchServerAnnouncement(Loc.GetString(Paused
                ? "game-ticker-pause-start"
                : "game-ticker-pause-start-resumed"));

            return true;
        }

        public bool TogglePause()
        {
            PauseStart(!Paused);
            return Paused;
        }

        private bool HasEnoughPlayersForLobbyStart()
        {
            return LobbyMinimumPlayerGate.HasEnoughPlayers(_playerManager.PlayerCount, LobbyMinimumPlayers);
        }

        private void UpdateLobbyCountdownForPlayerCount(bool notify = true, bool resetCountdown = false)
        {
            if (!LobbyEnabled || RunLevel != GameRunLevel.PreRoundLobby)
                return;

            if (!HasEnoughPlayersForLobbyStart())
            {
                if (_roundStartCountdownHasNotStartedYetDueToNoPlayers && _roundStartTime == TimeSpan.Zero)
                    return;

                _roundStartCountdownHasNotStartedYetDueToNoPlayers = true;
                _roundStartTime = TimeSpan.Zero;
            }
            else
            {
                if (!resetCountdown && !_roundStartCountdownHasNotStartedYetDueToNoPlayers && _roundStartTime != TimeSpan.Zero)
                    return;

                _roundStartCountdownHasNotStartedYetDueToNoPlayers = false;
                _roundStartTime = _gameTiming.CurTime + LobbyDuration;
            }

            if (!notify)
                return;

            RaiseNetworkEvent(new TickerLobbyCountdownEvent(_roundStartTime, Paused));
            SendStatusToAll();
            UpdateInfoText();
        }

        public void ToggleReadyAll(bool ready)
        {
            var status = ready ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
            foreach (var playerUserId in _playerGameStatuses.Keys)
            {
                _playerGameStatuses[playerUserId] = status;
                if (!_playerManager.TryGetSessionById(playerUserId, out var playerSession))
                    continue;
                RaiseNetworkEvent(GetStatusMsg(playerSession), playerSession.Channel);
            }
        }

        public void ToggleReady(ICommonSession player, bool ready)
        {
            if (!_playerGameStatuses.ContainsKey(player.UserId))
                return;

            if (!_userDb.IsLoadComplete(player))
                return;

            if (RunLevel != GameRunLevel.PreRoundLobby)
            {
                return;
            }

            var status = ready ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
            _playerGameStatuses[player.UserId] = ready ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
            RaiseNetworkEvent(GetStatusMsg(player), player.Channel);
            // update server info to reflect new ready count
            UpdateInfoText();
        }

        public bool UserHasJoinedGame(ICommonSession session)
            => UserHasJoinedGame(session.UserId);

        public bool UserHasJoinedGame(NetUserId userId)
            => PlayerGameStatuses.TryGetValue(userId, out var status) && status == PlayerGameStatus.JoinedGame;
    }
}
