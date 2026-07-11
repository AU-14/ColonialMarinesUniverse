using System.Threading.Tasks;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameWindow;
using Content.Shared.Players;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.GameTicking
{
    [UsedImplicitly]
    public sealed partial class GameTicker
    {
        [ViewVariables]
        private TimeSpan _joinTimingWarnThreshold = TimeSpan.FromSeconds(5);

        [Dependency] private IPlayerManager _playerManager = default!;

        private void InitializePlayer()
        {
            Subs.CVar(_cfg, CCVars.GameJoinTimingWarnSeconds,
                seconds => _joinTimingWarnThreshold = TimeSpan.FromSeconds(Math.Max(0f, seconds)), true);

            _playerManager.PlayerStatusChanged += PlayerStatusChanged;
        }

        private async void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            var session = args.Session;

            if (_mind.TryGetMind(session.UserId, out var mindId, out var mind))
            {
                if (args.NewStatus != SessionStatus.Disconnected)
                {
                    _pvsOverride.AddSessionOverride(mindId.Value, session);
                }
            }

            DebugTools.Assert(session.GetMind() == mindId);

            switch (args.NewStatus)
            {
                case SessionStatus.Connected:
                {
                    LogSlowJoinTransition(session, "GameTicker received Connected");

                    AddPlayerToDb(args.Session.UserId.UserId);

                    // Always make sure the client has player data.
                    if (session.Data.ContentDataUncast == null)
                    {
                        var data = new ContentPlayerData(session.UserId, args.Session.Name);
                        data.Mind = mindId;
                        session.Data.ContentDataUncast = data;
                    }

                    // Make the player actually join the game.
                    // timer time must be > tick length
                    Timer.Spawn(0, () =>
                    {
                        LogSlowJoinTransition(session, "GameTicker calling JoinGame");
                        _playerManager.JoinGame(args.Session);
                    });

                    var record = await _db.GetPlayerRecordByUserId(args.Session.UserId);
                    var firstConnection = record != null &&
                                          Math.Abs((record.FirstSeenTime - record.LastSeenTime).TotalMinutes) < 1;

                    _chatManager.SendAdminAnnouncement(firstConnection
                        ? Loc.GetString("player-first-join-message", ("name", args.Session.Name))
                        : Loc.GetString("player-join-message", ("name", args.Session.Name)));

                    RaiseNetworkEvent(GetConnectionStatusMsg(), session.Channel);

                    if (firstConnection && _cfg.GetCVar(CCVars.AdminNewPlayerJoinSound))
                        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/newplayerping.ogg"),
                            Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false,
                            audioParams: new AudioParams { Volume = -5f });

                    break;
                }

                case SessionStatus.InGame:
                {
                    var inGameAt = DateTime.UtcNow;
                    LogSlowJoinTransition(session, "GameTicker received InGame");

                    if (mind == null)
                    {
                        if (LobbyEnabled)
                        {
                            PlayerJoinLobby(session, inGameAt);
                            StartUserDataLoad(session);
                        }
                        else
                        {
                            StartUserDataLoad(session);
                            SpawnWaitDb();
                        }

                        break;
                    }

                    StartUserDataLoad(session);

                    if (mind.CurrentEntity == null || Deleted(mind.CurrentEntity))
                    {
                        DebugTools.Assert(mind.CurrentEntity == null, "a mind's current entity was deleted without updating the mind");

                        // This player is joining the game with an existing mind, but the mind has no entity.
                        // Their entity was probably deleted sometime while they were disconnected, or they were an observer.
                        // Instead of allowing them to spawn in, we will dump and their existing mind in an observer ghost.
                        SpawnObserverWaitDb();
                    }
                    else
                    {
                        if (_playerManager.SetAttachedEntity(session, mind.CurrentEntity))
                        {
                            PlayerJoinGame(session);
                        }
                        else
                        {
                            Log.Error(
                                $"Failed to attach player {session} with mind {ToPrettyString(mindId)} to its current entity {ToPrettyString(mind.CurrentEntity)}");
                            SpawnObserverWaitDb();
                        }
                    }

                    break;
                }

                case SessionStatus.Disconnected:
                {
                    _chatManager.SendAdminAnnouncement(Loc.GetString("player-leave-message", ("name", args.Session.Name)));
                    if (mindId != null)
                    {
                        _pvsOverride.RemoveSessionOverride(mindId.Value, session);
                    }

                    _userDb.ClientDisconnected(session);
                    break;
                }
            }

            UpdateLobbyCountdownForPlayerCount();

            //When the status of a player changes, update the server info text
            UpdateInfoText();

            async void SpawnWaitDb()
            {
                try
                {
                    await WaitUserDataLoad(session, "GameTicker waiting to spawn player");
                }
                catch (OperationCanceledException)
                {
                    // Bail, user must've disconnected or something.
                    Log.Debug($"Database load cancelled while waiting to spawn {session}");
                    return;
                }

                SpawnPlayer(session, EntityUid.Invalid);
            }

            async void SpawnObserverWaitDb()
            {
                try
                {
                    await WaitUserDataLoad(session, "GameTicker waiting to spawn observer");
                }
                catch (OperationCanceledException)
                {
                    // Bail, user must've disconnected or something.
                    Log.Debug($"Database load cancelled while waiting to spawn {session}");
                    return;
                }

                JoinAsObserver(session);
            }

            async void AddPlayerToDb(Guid id)
            {
                if (RoundId != 0 && _runLevel != GameRunLevel.PreRoundLobby)
                {
                    await _db.AddRoundPlayers(RoundId, id);
                }
            }
        }

        private void StartUserDataLoad(ICommonSession session)
        {
            var stopwatch = Stopwatch.StartNew();
            _userDb.ClientConnected(session);
            LogSlowJoinPhase(session, "GameTicker starting user data load", stopwatch.Elapsed);
        }

        private async Task WaitUserDataLoad(ICommonSession session, string step)
        {
            var stopwatch = Stopwatch.StartNew();
            await _userDb.WaitLoadComplete(session);
            LogSlowJoinPhase(session, step, stopwatch.Elapsed);
        }

        private void LogSlowJoinTransition(
            ICommonSession session,
            string step,
            DateTime? start = null,
            string baseline = "Connected status was set")
        {
            var elapsed = DateTime.UtcNow - (start ?? session.ConnectedTime);
            if (elapsed < _joinTimingWarnThreshold)
            {
                Log.Debug(
                    "[JOIN-TIMING] {Step} for {Player} {Elapsed:N0} ms after {Baseline}",
                    step,
                    session,
                    elapsed.TotalMilliseconds,
                    baseline);
                return;
            }

            Log.Warning(
                "[JOIN-TIMING] {Step} for {Player} {Elapsed:N0} ms after {Baseline}",
                step,
                session,
                elapsed.TotalMilliseconds,
                baseline);
        }

        private void LogSlowJoinPhase(ICommonSession session, string step, TimeSpan elapsed)
        {
            if (elapsed < _joinTimingWarnThreshold)
            {
                Log.Debug(
                    "[JOIN-TIMING] {Step} for {Player} took {Elapsed:N0} ms",
                    step,
                    session,
                    elapsed.TotalMilliseconds);
                return;
            }

            Log.Warning(
                "[JOIN-TIMING] {Step} for {Player} took {Elapsed:N0} ms",
                step,
                session,
                elapsed.TotalMilliseconds);
        }

        public HumanoidCharacterProfile GetPlayerProfile(ICommonSession p)
        {
            return (HumanoidCharacterProfile) _prefsManager.GetPreferences(p.UserId).SelectedCharacter;
        }

        public void PlayerJoinGame(ICommonSession session, bool silent = false)
        {
            LogSlowJoinTransition(session, "GameTicker sending game join");

            if (!silent)
                _chatManager.DispatchServerMessage(session, Loc.GetString("game-ticker-player-join-game-message"));

            _playerGameStatuses[session.UserId] = PlayerGameStatus.JoinedGame;
            _db.AddRoundPlayers(RoundId, session.UserId);

            if (_adminManager.HasAdminFlag(session, AdminFlags.Admin))
            {
                if (_allPreviousGameRules.Count > 0)
                {
                    var rulesMessage = GetGameRulesListMessage(true);
                    _chatManager.SendAdminAnnouncementMessage(session, Loc.GetString("starting-rule-selected-preset", ("preset", rulesMessage)));
                }
            }

            RaiseNetworkEvent(new TickerJoinGameEvent(), session.Channel);
            RaiseNetworkEvent(GetRoundStatusMsg(), session.Channel);
        }

        private void PlayerJoinLobby(ICommonSession session, DateTime? initialInGameAt = null)
        {
            if (initialInGameAt != null)
            {
                LogSlowJoinTransition(session, "GameTicker sending initial lobby join");
                LogSlowJoinTransition(
                    session,
                    "GameTicker post-InGame lobby handoff",
                    initialInGameAt,
                    "InGame status was set");
            }

            _playerGameStatuses[session.UserId] = LobbyEnabled ? PlayerGameStatus.NotReadyToPlay : PlayerGameStatus.ReadyToPlay;

            var client = session.Channel;
            RaiseNetworkEvent(new TickerJoinLobbyEvent(), client);
            RaiseNetworkEvent(GetStatusMsg(session), client);
            RaiseNetworkEvent(GetInfoMsg(), client);
            RaiseNetworkEvent(GetRoundStatusMsg(), client);
            RaiseLocalEvent(new PlayerJoinedLobbyEvent(session));
            _db.AddRoundPlayers(RoundId, session.UserId);
        }

        private void ReqWindowAttentionAll()
        {
            RaiseNetworkEvent(new RequestWindowAttentionEvent());
        }
    }

    public sealed partial class PlayerJoinedLobbyEvent : EntityEventArgs
    {
        public readonly ICommonSession PlayerSession;

        public PlayerJoinedLobbyEvent(ICommonSession playerSession)
        {
            PlayerSession = playerSession;
        }
    }
}
