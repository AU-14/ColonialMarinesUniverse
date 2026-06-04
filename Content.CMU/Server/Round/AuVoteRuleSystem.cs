using Content.Server.GameTicking.Rules;
using Content.Server.Voting.Managers;
using Content.Shared.GameTicking.Components;
using Content.Shared.Voting;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Station.Components;
using Content.Server.Voting;
using Content.Shared._RMC14.Rules;
using Content.Shared.GameTicking;
using Content.Shared.AU14;
using Content.Shared._RMC14.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;
// ok so this is AI slopcode but I will refine it later (probably) - eg




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



    protected override void Started(EntityUid uid, AuVoteRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        // No vote call here; only after restart cleanup.
        var auRoundSystem = _entityManager.System<AuRoundSystem>();
        var sawmill = Logger.GetSawmill("game");
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var mapLoader = _entityManager.EntitySysManager.GetEntitySystem<MapLoaderSystem>();
        var mapSystem = _entityManager.EntitySysManager.GetEntitySystem<MapSystem>();
        //auRoundSystem.LoadSelectedPlanetMap();

    }
}
