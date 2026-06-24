using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Kill-all rule that targets all humanoid mobs (any entity with HumanoidAppearanceComponent),
///     excluding xenos. Evacuated entities are excluded from the count entirely.
/// </summary>
public sealed partial class KillAllHumanRuleSystem : GameRuleSystem<KillAllHumanRuleComponent>
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;

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
        if (_gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>())
            return;

        if (ev.NewMobState != MobState.Dead)
            return;

        if (!HasComp<HumanoidAppearanceComponent>(ev.Target))
            return;

        CheckVictoryCondition();
    }

    /// <summary>
    ///     Called by KillAllRulesHandcuffSystem when a human entity is handcuffed.
    /// </summary>
    public void OnHandcuffEvent(EntityUid _) => CheckVictoryCondition();

    private bool IsInArrestArea(EntityUid uid) => _area.TryGetArea(uid, out Entity<AreaComponent>? area, out _)
     && area.Value.Comp.CountAsArrestedForEndConditions;

    private void OnJumpsuitChanged(EntityUid wearer, string slot, EntityUid equipment)
    {
        if (slot != "jumpsuit" || Prototype(equipment)?.ID != "AU14CivilianPrisonJumpsuit")
            return;

        if (!_gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>())
            return;

        if (!HasComp<HumanoidAppearanceComponent>(wearer))
            return;

        CheckVictoryCondition();
    }

    private bool HasPrisonJumpsuit(EntityUid uid) => _inventory.TryGetSlotEntity(uid, "jumpsuit", out EntityUid? suit)
     && Prototype(suit!.Value)?.ID == "AU14CivilianPrisonJumpsuit";

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllHumanRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();

        if (!queryRule.MoveNext(out _, out _, out KillAllHumanRuleComponent? ruleComp, out _))
            return;
        if (ruleComp == null) return;

        int  requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        bool countArrests    = ruleComp.Arrest;
        bool crashedDropship = HasCrashedDropship();

        // Count all humanoid mobs (excluding xenos and evacuated)
        var total      = 0;
        var eliminated = 0;

        EntityQueryEnumerator<MobStateComponent, HumanoidAppearanceComponent> query
            = EntityQueryEnumerator<MobStateComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState, out _))
        {
            if (IsExcludedFromVictory(uid, mobState))
                continue;

            // If the entity's grid has been evacuated, count them as dead (do not skip)
            if (IsEvacuated(uid))
            {
                total++;
                eliminated++;

                continue;
            }

            if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)) && mobState.CurrentState != MobState.Dead)
                continue;

            total++;

            if (mobState.CurrentState == MobState.Dead)
                eliminated++;

            // Wearing jumpsuit, or arrested flag is set and they're cuffed, or in the mapped brig areas
            else if (HasPrisonJumpsuit(uid)
                  || (countArrests && ((TryComp(uid, out CuffableComponent? cuffable)
                          && cuffable.CuffedHandCount > 0)
                      || IsInArrestArea(uid))))
                eliminated++;
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
                    _gameTicker.EndRound("Threat victory: Required percentage of humans eliminated.");
            }
        }
    }
}