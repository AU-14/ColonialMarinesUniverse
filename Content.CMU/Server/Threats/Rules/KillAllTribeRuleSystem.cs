using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Evacuation;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using KillAllTribeRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllTribeRuleComponent;
using TribalComponent = Content.Shared._CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class KillAllTribeRuleSystem : GameRuleSystem<KillAllTribeRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private EntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    private const string DefaultWinMsg = "The Threat has been Eliminated";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllTribeRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllTribeRuleComponent>())
            return;
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllTribeRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllTribeRuleComponent ruleComp, out _))
            return;

        int requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        int eliminated = 0, total = 0;

        EntityQueryEnumerator<MobStateComponent> query = _entMan.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState))
        {
            if (!_entMan.HasComponent<TribalComponent>(uid))
                continue;

            total++;
            if (mobState.CurrentState == MobState.Dead || _threatRuleHelper.IsEvacuated(uid))
                eliminated++;
        }

        if (total == 0)
            return;
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;
        if (!ThreatRuleHelper.MeetsRequiredPercent(eliminated, total, requiredPercent))
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage) ? winMessage : DefaultWinMsg);
    }
}
