using System.Linq;
using Content.Shared.Cuffs.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.SSDIndicator;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14;

namespace Content.Server.AU14.Threats;

/// <summary>
/// Kill-all rule that targets all Govfor, excludes SSD and evacuated.
/// Govfor wearing a prisoner jumpsuit, or handcuffed, or inside brig, or dead are eliminated.
/// </summary>
public sealed partial class KillAllGovforRuleSystem : GameRuleSystem<KillAllGovforRuleComponent>
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private Round.AuRoundSystem _auRoundSystem = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
        SubscribeLocalEvent<GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(GotEquippedEvent ev) => OnJumpsuitChanged(ev.Equipee, ev.Slot, ev.Equipment);
    private void OnGotUnequipped(GotUnequippedEvent ev) => OnJumpsuitChanged(ev.Equipee, ev.Slot, ev.Equipment);

    private bool IsEvacuated(EntityUid uid)
    {
        return Transform(uid).GridUid is { } grid && _evacuatedQuery.HasComp(grid);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!IsActiveRuleAndGovfor(ev.Target) || ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    /// <summary>
    /// Called by KillAllRulesHandcuffSystem when a Govfor entity is handcuffed.
    /// </summary>
    public void OnHandcuffEvent(EntityUid _) => CheckVictoryCondition();

    private bool IsInArrestArea(EntityUid uid)
    {
        return _area.TryGetArea(uid, out var area, out _) && area.Value.Comp.CountAsArrestedForEndConditions;
    }

    private void OnJumpsuitChanged(EntityUid wearer, string slot, EntityUid equipment)
    {
        if (slot != "jumpsuit" || Prototype(equipment)?.ID != "AU14CivilianPrisonJumpsuit")
            return;

        if (!IsActiveRuleAndGovfor(wearer))
            return;

        CheckVictoryCondition();
    }

    private bool HasPrisonJumpsuit(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "jumpsuit", out var suit)
            && Prototype(suit!.Value)?.ID == "AU14CivilianPrisonJumpsuit";
    }

    private bool IsActiveRuleAndGovfor(EntityUid uid)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>())
            return false;

        return TryComp<NpcFactionMemberComponent>(uid, out var faction)
            && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor");
    }

    private bool IsExcludedFromKillCount(EntityUid uid, MobStateComponent mobState)
    {
        // Don't exclude the dead (ghosts), we tally them as eliminated instead
        if (mobState.CurrentState == MobState.Dead)
            return false;

        return (HasComp<XenoNestedComponent>(uid) || HasComp<SynthComponent>(uid)
            || (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD));
    }

    private void CheckVictoryCondition()
    {
        var queryRules = QueryActiveRules();
        if (!queryRules.MoveNext(out _, out _, out var ruleComp, out _))
            return;
        if (ruleComp == null) return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        var countArrests = ruleComp.Arrest;

        // Count total and dead/arrested Govfor mobs (excluding evacuated)
        var total = 0;
        var eliminated = 0;

        var query = EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor"))
            {
                if (IsExcludedFromKillCount(uid, mobState))
                    continue;

                // If the grid was evacuated, count them as dead (do not skip)
                if (IsEvacuated(uid))
                {
                    total++;
                    eliminated++;
                    continue;
                }

                total++;

                if (mobState.CurrentState == MobState.Dead)
                    eliminated++;
                // Wearing jumpsuit, or arrested flag is set and they're cuffed, or in the mapped brig areas
                else if (HasPrisonJumpsuit(uid)
                    || countArrests && ((TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
                    || IsInArrestArea(uid)))
                {
                    eliminated++;
                }
            }
        }

        if (total == 0)
            return;

        var percentEliminated = (int)((double)eliminated / total * 100.0);

        if (percentEliminated >= requiredPercent)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            var customMessage = ruleComp.WinMessage;
            if (!string.IsNullOrEmpty(customMessage))
            {
                _gameTicker.EndRound(customMessage);
            }
            else
            {
                var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
                if (!string.IsNullOrEmpty(winMessage))
                {
                    _gameTicker.EndRound(winMessage);
                }
                else
                {
                    _gameTicker.EndRound("Threat victory: Required percentage of Govfor eliminated.");
                }
            }
        }
    }
}
