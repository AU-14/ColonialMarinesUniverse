using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared._RMC14.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.AU14.Round;

public sealed partial class AuVoteRuleSystem : GameRuleSystem<AuVoteRuleComponent>
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private CMURoundDirectorSystem _roundDirector = default!;

    private bool _pausedForMinimumPlayers;
    private bool _waitingForMinimumPlayers;

    // Only keep the persistent system trigger and dependency injection
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(
            OnRoundRestartCleanup,
            after: [typeof(CMURoundDirectorSystem)]);
        SubscribeLocalEvent<AuVotePlayerCountChangedEvent>(OnPlayerCountChanged);
        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
    }


    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        TryStartVoteSequence(roundRestart: true);
    }

    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (!_waitingForMinimumPlayers)
            return;

        // PlayerStatusChanged can be raised before PlayerCount includes the newly connected session.
        // Queue the check so the twentieth player reliably starts the lobby vote sequence.
        QueueLocalEvent(new AuVotePlayerCountChangedEvent());
    }

    private void OnPlayerCountChanged(AuVotePlayerCountChangedEvent ev)
    {
        if (!_waitingForMinimumPlayers)
            return;

        TryStartVoteSequence();
    }

    private void TryStartVoteSequence()
    {
        TryStartVoteSequence(roundRestart: false);
    }

    private void TryStartVoteSequence(bool roundRestart)
    {
        var acceptingSelections = roundRestart ||
                                  _roundDirector.Phase == CMURoundPhase.AwaitingSelection;
        if (!AuLobbyVoteGate.ShouldStartVoteSequence(
                GameTicker.LobbyEnabled,
                GameTicker.RunLevel,
                acceptingSelections,
                _playerManager.PlayerCount,
                _cfg.GetCVar(RMCCVars.RMCLobbyMinimumPlayers)))
        {
            _waitingForMinimumPlayers = GameTicker.LobbyEnabled &&
                                        GameTicker.RunLevel == GameRunLevel.PreRoundLobby &&
                                        acceptingSelections;
            if (_waitingForMinimumPlayers)
                PauseForMinimumPlayers();
            return;
        }

        _waitingForMinimumPlayers = false;
        var voteManagerSystem = _entityManager.System<AuRoundSystem>();
        voteManagerSystem.StartVoteSequence();
        RestartCountdownAfterMinimumPlayers();
    }

    private void PauseForMinimumPlayers()
    {
        if (_pausedForMinimumPlayers || GameTicker.Paused)
            return;

        GameTicker.PauseStart();
        _pausedForMinimumPlayers = !_cfg.GetCVar(RMCCVars.RMCLobbyStartPaused);
    }

    private void RestartCountdownAfterMinimumPlayers()
    {
        if (!_pausedForMinimumPlayers)
            return;

        _pausedForMinimumPlayers = false;
        GameTicker.RestartLobbyCountdown();
    }
}

internal sealed class AuVotePlayerCountChangedEvent : EntityEventArgs;
