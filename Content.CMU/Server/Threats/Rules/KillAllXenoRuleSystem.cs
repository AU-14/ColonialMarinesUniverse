using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using CultistComponent = Content.Shared._CMU14.Threats.Mobs.Cultist.CultistComponent;
using KillAllXenoRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllXenoRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class KillAllXenoRuleSystem : GameRuleSystem<KillAllXenoRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;

    private static readonly ProtoId<JobPrototype> LesserDroneRole = "CMXenoLesserDrone";
    private const string DefaultWinMsg = "The threat has been eliminated!";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllXenoRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllXenoRuleComponent>())
            return;

        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllXenoRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllXenoRuleComponent ruleComp, out _))
            return;

        int requiredPercentXeno = Math.Clamp(ruleComp.PercentXeno, 1, 100);
        int requiredPercentCultist = Math.Clamp(ruleComp.PercentCultist, 1, 100);
        int totalXeno = 0, deadXeno = 0;
        int totalCultist = 0, deadCultist = 0;

        EntityQueryEnumerator<MobStateComponent> query = _entMan.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState))
        {
            if (_entMan.TryGetComponent(uid, out XenoComponent? xeno)
                && xeno.Role != LesserDroneRole)
            {
                totalXeno++;
                if (mobState.CurrentState == MobState.Dead || _threatRuleHelper.IsEvacuated(uid))
                    deadXeno++;
            }

            if (_entMan.HasComponent<CultistComponent>(uid))
            {
                totalCultist++;
                if (mobState.CurrentState == MobState.Dead || _threatRuleHelper.IsEvacuated(uid))
                    deadCultist++;
                else if (_entMan.TryGetComponent(uid, out CuffableComponent? cuff) && cuff.CuffedHandCount > 0)
                    deadCultist++;
            }
        }

        if (totalXeno == 0)
            return;
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        bool xenoSatisfied = ThreatRuleHelper.MeetsRequiredPercent(deadXeno, totalXeno, requiredPercentXeno);
        bool cultistSatisfied
            = ThreatRuleHelper.MeetsRequiredPercent(deadCultist, totalCultist, requiredPercentCultist);
        if (!xenoSatisfied || !cultistSatisfied)
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        _gameTicker.EndRound(string.IsNullOrEmpty(winMessage) ? DefaultWinMsg : winMessage);
    }
}
