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
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;
// ok so this is AI slopcode but I will refine it later (probably) - eg




public sealed class AuVoteRuleSystem : GameRuleSystem<AuVoteRuleComponent>
{
        [Dependency] private readonly IEntityManager _entityManager = default!;

    // Only keep the persistent system trigger and dependency injection
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }


    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        var voteManagerSystem = _entityManager.System<AuRoundSystem>();
        // Always reset and start the vote sequence after every round restart
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
