using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Kill-all rule that targets all CLF faction members, excludes SSD and evacuated.
///     CLF wearing a prisoner jumpsuit, or handcuffed, or inside brig, or dead are eliminated.
/// </summary>
public sealed partial class KillAllClfRuleSystem : GameRuleSystem<KillAllClfRuleComponent>
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
        SubscribeLocalEvent<GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(GotEquippedEvent     ev) => OnJumpsuitChanged(ev.Equipee, ev.Slot, ev.Equipment);
    private void OnGotUnequipped(GotUnequippedEvent ev) => OnJumpsuitChanged(ev.Equipee, ev.Slot, ev.Equipment);

    private bool IsEvacuated(EntityUid uid) => Transform(uid).GridUid is { } grid && _evacuatedQuery.HasComp(grid);

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!IsActiveRuleAndCLF(ev.Target) || ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    /// <summary>
    ///     Called by KillAllRulesHandcuffSystem when a CLF entity is handcuffed.
    /// </summary>
    public void OnHandcuffEvent(EntityUid _) => CheckVictoryCondition();

    private bool IsInArrestArea(EntityUid uid) => _area.TryGetArea(uid, out Entity<AreaComponent>? area, out _)
     && area.Value.Comp.CountAsArrestedForEndConditions;

    private void OnJumpsuitChanged(EntityUid wearer, string slot, EntityUid equipment)
    {
        if (slot != "jumpsuit" || Prototype(equipment)?.ID != "AU14CivilianPrisonJumpsuit")
            return;

        if (!IsActiveRuleAndCLF(wearer))
            return;

        CheckVictoryCondition();
    }

    private bool HasPrisonJumpsuit(EntityUid uid) => _inventory.TryGetSlotEntity(uid, "jumpsuit", out EntityUid? suit)
     && Prototype(suit!.Value)?.ID == "AU14CivilianPrisonJumpsuit";

    private bool IsActiveRuleAndCLF(EntityUid uid)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
            return false;

        return TryComp<NpcFactionMemberComponent>(uid, out NpcFactionMemberComponent? faction)
         && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "clf");
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllClfRuleComponent, GameRuleComponent> queryRules
            = QueryActiveRules();

        if (!queryRules.MoveNext(out _, out _, out KillAllClfRuleComponent? ruleComp, out _))
            return;
        if (ruleComp == null) return;

        int  requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        bool countArrests    = ruleComp.Arrest;
        bool crashedDropship = HasCrashedDropship(); // FIX in next commit

        // Count total and dead/arrested CLF mobs (excluding evacuated)
        var total      = 0;
        var eliminated = 0;

        EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent> query
            = EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState,
                   out NpcFactionMemberComponent? faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "clf"))
            {
                if (IsExcludedFromVictory(uid, mobState))
                    continue;

                // Skip evacuated entities entirely
                if (IsEvacuated(uid))
                    continue;

                if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)) && mobState.CurrentState != MobState.Dead)
                    continue;

                total++;

                if (mobState.CurrentState == MobState.Dead)
                    eliminated++;

                // Wearing jumpsuit, or arrested flag is set and they're cuffed, or in the mapped brig areas
                else if (HasPrisonJumpsuit(uid)
                      || (countArrests && ((TryComp<CuffableComponent>(uid, out CuffableComponent? cuffable)
                              && cuffable.CuffedHandCount > 0)
                          || IsInArrestArea(uid))))
                    eliminated++;
            }
        }

        if (total == 0)
            return;

        var percentEliminated = (int)((double)eliminated / total * 100.0);

        if (percentEliminated >= requiredPercent)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            string? customMessage = ruleComp.WinMessage;
            if (!string.IsNullOrEmpty(customMessage))
                _gameTicker.EndRound(customMessage);
            else
            {
                string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
                if (!string.IsNullOrEmpty(winMessage))
                    _gameTicker.EndRound(winMessage);
                else
                    _gameTicker.EndRound("Govfor victory: Required percentage of CLF eliminated.");
            }
        }
    }
}