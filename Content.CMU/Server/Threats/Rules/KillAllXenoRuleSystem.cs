using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.AU14;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class KillAllXenoRuleSystem : GameRuleSystem<KillAllXenoRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private bool IsEvacuated(EntityUid uid)
    {
        TransformComponent xform = Transform(uid);

        return xform.GridUid is { } grid && _evacuatedQuery.HasComp(grid);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllXenoRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllXeno rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllXenoRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllXenoRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();

        if (!queryRule.MoveNext(out _, out _, out KillAllXenoRuleComponent? ruleComp, out _))
            return;
        if (ruleComp == null) return;

        int requiredPercentXeno    = Math.Clamp(ruleComp.PercentXeno, 1, 100);
        int requiredPercentCultist = Math.Clamp(ruleComp.PercentCultist, 1, 100);

        // Count total and dead Xeno and Cultist mobs separately (excluding evacuated)
        var totalXeno    = 0;
        var deadXeno     = 0;
        var totalCultist = 0;
        var deadCultist  = 0;

        EntityQueryEnumerator<MobStateComponent> query = _entityManager.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState))
        {
            if (_entityManager.TryGetComponent(uid, out XenoComponent? xeno))
            {
                if (xeno.Role == "CMXenoLesserDrone")
                    continue;

                totalXeno++;

                // Treat evacuated entities as dead for victory conditions
                if (IsEvacuated(uid) || mobState.CurrentState == MobState.Dead)
                    deadXeno++;
            }

            if (_entityManager.HasComponent<CultistComponent>(uid))
            {
                totalCultist++;

                // Treat evacuated entities as dead; otherwise count actual death or restraints.
                if (IsEvacuated(uid) || mobState.CurrentState == MobState.Dead)
                    deadCultist++;
                else if (_entityManager.TryGetComponent(uid, out CuffableComponent? cuff) && cuff.CuffedHandCount > 0)
                {
                    // Restrained cultist counts as killed for the purposes of this rule.
                    deadCultist++;
                }
            }
        }

        // If nothing to count at all, bail out
        if (totalXeno == 0 && totalCultist == 0)
            return;

        // Calculate percent dead for each category. If a category has zero total we treat it as satisfied.
        int percentDeadXeno    = totalXeno == 0 ? 100 : (int)((double)deadXeno / totalXeno * 100.0);
        int percentDeadCultist = totalCultist == 0 ? 100 : (int)((double)deadCultist / totalCultist * 100.0);

        bool xenoSatisfied    = percentDeadXeno >= requiredPercentXeno;
        bool cultistSatisfied = percentDeadCultist >= requiredPercentCultist;

        if (xenoSatisfied && cultistSatisfied)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            // Prefer any configured win message, otherwise use a default.
            string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
                _gameTicker.EndRound(winMessage);
            else
                _gameTicker.EndRound("The Threat has been Eliminated");
        }
    }
}