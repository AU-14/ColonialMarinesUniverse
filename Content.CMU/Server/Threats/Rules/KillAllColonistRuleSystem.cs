using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14;
using Content.Shared.AU14.ColonyEvacuation;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Kill-all rule that targets all Colonists, excludes SSD.
///     Colonists wearing a prisoner jumpsuit, or handcuffed, or inside brig, or dead are eliminated.
/// </summary>
public sealed partial class KillAllColonistRuleSystem : GameRuleSystem<KillAllColonistRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private bool IsEvacuated(EntityUid uid) => Transform(uid).GridUid is { } grid && _evacuatedQuery.HasComp(grid);

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!IsActiveRuleAndColonist(ev.Target) || ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private bool IsActiveRuleAndColonist(EntityUid uid)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            return false;

        return TryComp<NpcFactionMemberComponent>(uid, out NpcFactionMemberComponent? faction)
         && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist");
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllColonistRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();

        if (!queryRule.MoveNext(out _, out _, out KillAllColonistRuleComponent? ruleComp, out _))
            return;
        if (ruleComp == null) return;

        int  requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        bool crashedDropship = HasCrashedDropship();

        // Count total and dead AUColonist mobs (excluding evacuated)
        var total = 0;
        var dead  = 0;

        EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent> query
            = EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState,
                   out NpcFactionMemberComponent? faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist"))
            {
                if (IsExcludedFromVictory(uid, mobState))
                    continue;

                // If the entity's grid was evacuated, count them as dead (do not skip)
                if (IsEvacuated(uid))
                {
                    total++;
                    dead++;

                    continue;
                }

                if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)) && mobState.CurrentState != MobState.Dead)
                    continue;

                total++;
                if (mobState.CurrentState == MobState.Dead)
                    dead++;
            }
        }

        if (total == 0)
            return;

        var percentDead = (int)((double)dead / total * 100.0);

        if (!ruleComp.ColonyEvacTriggered &&
            ruleComp.ColonyEvacThreshold > 0 &&
            percentDead >= ruleComp.ColonyEvacThreshold)
        {
            ruleComp.ColonyEvacTriggered = true;
            var evacEv = new ColonyWithdrawEvacEnabledEvent();
            RaiseLocalEvent(ref evacEv);
        }

        if (percentDead >= requiredPercent)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
                _gameTicker.EndRound(winMessage);
            else
                _gameTicker.EndRound("Threat victory: Required percentage of Colonists eliminated.");
        }
    }
}