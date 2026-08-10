using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.Maps;
using Content.Shared.CMU.Round;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;

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

            var selectedMap = _gameMapManager.GetSelectedMap();
            if (!string.IsNullOrWhiteSpace(selectedMap?.MapName))
                return selectedMap.MapName;

            if (!string.IsNullOrWhiteSpace(selectedPlanet?.MapId))
                return selectedPlanet.MapId;

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
            var govforPlatoon = _cmuRoundDirector.TryGetLegacyForceProjection(
                RoundSide.Govfor,
                out var govforForce)
                ? govforForce.Name
                : null;
            var opforPlatoon = _cmuRoundDirector.TryGetLegacyForceProjection(
                RoundSide.Opfor,
                out var opforForce)
                ? opforForce.Name
                : null;
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

        private CMURoundStatusEvent GetRoundStatusMsg()
        {
            var preset = CurrentPreset ?? Preset;
            var gamemodeTitle = preset != null
                ? LocalizeOrRaw(preset.ModeTitle)
                : Loc.GetString("ui-escape-status-unknown");

            return new CMURoundStatusEvent(
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
