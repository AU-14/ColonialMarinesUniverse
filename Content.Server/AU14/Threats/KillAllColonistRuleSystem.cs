using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.SSDIndicator;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14;
using Content.Shared.AU14.ColonyEvacuation;

namespace Content.Server.AU14.Threats;

/// <summary>
/// Kill-all rule that targets all Colonists, excludes SSD.
/// Colonists wearing a prisoner jumpsuit, or handcuffed, or inside brig, or dead are eliminated.
/// </summary>
public sealed partial class KillAllColonistRuleSystem : GameRuleSystem<KillAllColonistRuleComponent>
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private Round.AuRoundSystem _auRoundSystem = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;

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
        return Transform(uid).GridUid is { } grid && _evacuatedQuery.HasComp(grid);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            CheckVictoryCondition();
    }

    private bool HasCrashedDropship()
    {
        var dropships = EntityQueryEnumerator<DropshipComponent>();
        while (dropships.MoveNext(out _, out var dropship))
        {
            if (dropship.Crashed)
                return true;
        }

        return false;
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

        return TryComp<NpcFactionMemberComponent>(uid, out var faction)
            && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist");
    }

    private bool IsExcludedFromKillCount(EntityUid uid, MobStateComponent mobState)
    {
        // Don't exclude the dead (ghosts), we tally them as eliminated instead
        if (mobState.CurrentState == MobState.Dead)
            return false;

        return HasComp<XenoNestedComponent>(uid) || HasComp<SynthComponent>(uid)
            || (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD);
    }

    private void CheckVictoryCondition()
    {
        var queryRule = QueryActiveRules();
        if (!queryRule.MoveNext(out _, out _, out var ruleComp, out _))
            return;
        if (ruleComp == null) return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        var crashedDropship = HasCrashedDropship();

        // Count total and dead AUColonist mobs (excluding evacuated)
        var total = 0;
        var dead = 0;

        var query = EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist"))
            {
                if (IsExcludedFromKillCount(uid, mobState))
                    continue;

                if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)))
                    continue;

                // If the entity's grid was evacuated, count them as dead (do not skip)
                if (IsEvacuated(uid))
                {
                    total++;
                    dead++;
                    continue;
                }

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

            var winMessage = _auRoundSystem._selectedthreat.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
            {
                _gameTicker.EndRound(winMessage);
            }
            else
            {
                _gameTicker.EndRound("Threat victory: Required percentage of Colonists eliminated.");
            }
        }
    }
}
