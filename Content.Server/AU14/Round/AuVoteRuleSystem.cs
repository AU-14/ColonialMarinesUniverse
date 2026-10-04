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

    private bool _waitingForMinimumPlayers;

    // Only keep the persistent system trigger and dependency injection
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
    }


    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        TryStartVoteSequence();
    }

    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (!_waitingForMinimumPlayers)
            return;

        TryStartVoteSequence();
    }

    private void TryStartVoteSequence()
    {
        if (!AuLobbyVoteGate.ShouldStartVoteSequence(
                GameTicker.LobbyEnabled,
                GameTicker.RunLevel,
                _playerManager.PlayerCount,
                _cfg.GetCVar(RMCCVars.RMCLobbyMinimumPlayers)))
        {
            _waitingForMinimumPlayers = GameTicker.LobbyEnabled &&
                                        GameTicker.RunLevel == GameRunLevel.PreRoundLobby;
            return;
        }

        _waitingForMinimumPlayers = false;
        var voteManagerSystem = _entityManager.System<AuRoundSystem>();
        voteManagerSystem.StartVoteSequence(() => {});
    }
}
