using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using KillAllHumanRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllHumanRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Kill-all rule that targets all humanoid mobs (any entity with HumanoidAppearanceComponent),
///     excluding xenos. Evacuated entities are excluded from the count entirely.
/// </summary>
public sealed partial class KillAllHumanRuleSystem : GameRuleSystem<KillAllHumanRuleComponent>
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    private const string DefaultWinMsg = "Threat victory: Required percentage of humans eliminated.";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
        SubscribeLocalEvent<GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(GotEquippedEvent ev) => OnJumpsuitChanged(ev.Equipee, ev.Slot, ev.Equipment);
    private void OnGotUnequipped(GotUnequippedEvent ev) => OnJumpsuitChanged(ev.Equipee, ev.Slot, ev.Equipment);
    public void OnHandcuffEvent(EntityUid _) => CheckVictoryCondition();

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

    private bool HasPrisonJumpsuit(EntityUid uid)
        => _inventory.TryGetSlotEntity(uid, "jumpsuit", out EntityUid? suit)
            && Prototype(suit!.Value)?.ID == "AU14CivilianPrisonJumpsuit";

    private bool IsInArrestArea(EntityUid uid)
        => _area.TryGetArea(uid, out Entity<AreaComponent>? area, out _)
            && area.Value.Comp.CountAsArrestedForEndConditions;

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllHumanRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllHumanRuleComponent ruleComp, out _))
            return;

        int eliminated = 0, total = 0;
        int requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        bool countArrests = ruleComp.Arrest;
        bool crashedDropship = _threatRuleHelper.HasCrashedDropship();

        EntityQueryEnumerator<MobStateComponent, HumanoidAppearanceComponent> query = _entMan
            .EntityQueryEnumerator<MobStateComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState, out _))
        {
            if (_threatRuleHelper.IsExcludedFromVictory(uid, mobState))
                continue;

            total++;

            if (_threatRuleHelper.IsEvacuated(uid))
            {
                eliminated++;
                continue;
            }

            if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)) && mobState.CurrentState != MobState.Dead)
                continue;

            if (mobState.CurrentState == MobState.Dead)
                eliminated++;

            else if (HasPrisonJumpsuit(uid)
                || (countArrests && ((TryComp(uid, out CuffableComponent? cuffable) && cuffable.CuffedHandCount > 0)
                    || IsInArrestArea(uid))))
                eliminated++;
        }

        if (total == 0)
            return;
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;
        if (!ThreatRuleHelper.MeetsRequiredPercent(eliminated, total, requiredPercent))
            return;

        string? winMessage = !string.IsNullOrEmpty(ruleComp.WinMessage)
            ? ruleComp.WinMessage
            : _auRoundSystem.SelectedThreat?.WinMessage;

        _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage) ? winMessage : DefaultWinMsg);
    }
}
