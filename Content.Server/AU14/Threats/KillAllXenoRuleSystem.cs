using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.AU14;

namespace Content.Server.AU14.Threats;

public sealed partial class KillAllXenoRuleSystem : GameRuleSystem<KillAllXenoRuleComponent>
{
	[Dependency] private IEntityManager _entityManager = default!;
	[Dependency] private GameTicker _gameTicker = default!;
	[Dependency] private Round.AuRoundSystem _auRoundSystem = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private bool IsEvacuated(EntityUid uid)
    {
        var xform = Transform(uid);
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
        // Get the active rule entity and its component to read Percent
        var queryRule = EntityQueryEnumerator<KillAllXenoRuleComponent, GameRuleComponent>();
        if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
            return;

        var requiredPercentXeno = Math.Clamp(ruleComp.PercentXeno, 1, 100);
        var requiredPercentCultist = Math.Clamp(ruleComp.PercentCultist, 1, 100);

        // Count total and dead Xeno and Cultist mobs separately (excluding evacuated)
        var totalXeno = 0;
        var deadXeno = 0;
        var totalCultist = 0;
        var deadCultist = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var uid, out var mobState))
        {
            // Detect xenos by presence of XenoComponent, and cultists by CultistComponent.
            var isXeno = _entityManager.TryGetComponent(uid, out XenoComponent? xeno);
            var isCultist = _entityManager.HasComponent<CultistComponent>(uid);

            if (isXeno)
            {
                if (xeno!.Role == "CMXenoLesserDrone")
                    continue;

                totalXeno++;
                // Treat evacuated entities as dead for victory conditions
                if (IsEvacuated(uid) || mobState.CurrentState == MobState.Dead)
                    deadXeno++;
            }

            if (isCultist)
            {
                totalCultist++;
                // Treat evacuated entities as dead; otherwise count actual death or restraints.
                if (IsEvacuated(uid) || mobState.CurrentState == MobState.Dead)
                {
                    deadCultist++;
                }
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
        var percentDeadXeno = totalXeno == 0 ? 100 : (int)((double)deadXeno / totalXeno * 100.0);
        var percentDeadCultist = totalCultist == 0 ? 100 : (int)((double)deadCultist / totalCultist * 100.0);

        var xenoSatisfied = percentDeadXeno >= requiredPercentXeno;
        var cultistSatisfied = percentDeadCultist >= requiredPercentCultist;

        if (xenoSatisfied && cultistSatisfied)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            // Prefer any configured win message, otherwise use a default.
            var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
            {
                _gameTicker.EndRound(winMessage);
            }
            else
            {
                _gameTicker.EndRound("The Threat has been Eliminated");
            }
        }
    }
}
