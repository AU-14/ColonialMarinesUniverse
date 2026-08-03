using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Round;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
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
            RaiseNetworkEvent(GetInfoMsg(), Filter.Empty().AddPlayers(_playerManager.NetworkedSessions));
        }

        private string GetPlanetMapName()
        {
            var selectedPlanet = _auRoundSystem.GetSelectedPlanet();
            if (!string.IsNullOrWhiteSpace(selectedPlanet?.VoteName))
                return selectedPlanet.VoteName;

            var selectedMap = _gameMapManager.GetSelectedMap();
            if (!string.IsNullOrWhiteSpace(selectedMap?.MapName))
                return selectedMap.MapName;

            if (!string.IsNullOrWhiteSpace(selectedPlanet?.MapId))
                return selectedPlanet.MapId;

            return Loc.GetString("game-ticker-no-map-selected-plain");
        }

        private string LocalizeOrRaw(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return Loc.TryGetString(text, out var localized) ? localized : text;
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

            var govforShip = _auRoundSystem.GetSelectedGovforShip();
            var opforShip = _auRoundSystem.GetSelectedOpforShip();
            var govforPlatoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon?.Name;
            var opforPlatoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon?.Name;
            var gmTitle = LocalizeOrRaw((Decoy ?? preset).ModeTitle);
            var desc = LocalizeOrRaw((Decoy ?? preset).Description);
            return Loc.GetString(
                RunLevel == GameRunLevel.PreRoundLobby
                    ? "game-ticker-get-info-preround-text"
                    : "game-ticker-get-info-text",
                ("roundId", RoundId),
                ("playerCount", playerCount),
                ("readyCount", readyCount),
                ("planetName", GetPlanetMapName()),
                ("govforShip", string.IsNullOrWhiteSpace(govforShip) ? "None" : govforShip),
                ("opforShip", string.IsNullOrWhiteSpace(opforShip) ? "None" : opforShip),
                ("govforPlatoon", string.IsNullOrWhiteSpace(govforPlatoon) ? "None" : govforPlatoon),
                ("opforPlatoon", string.IsNullOrWhiteSpace(opforPlatoon) ? "None" : opforPlatoon),
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
            return new(GetInfoText());
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
