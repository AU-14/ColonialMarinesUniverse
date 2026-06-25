using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using KillAllClfRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllClfRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Kill-all rule that targets all CLF faction members, excludes SSD and evacuated.
///     CLF wearing a prisoner jumpsuit, or handcuffed, or inside brig, or dead are eliminated.
/// </summary>
public sealed partial class KillAllClfRuleSystem : GameRuleSystem<KillAllClfRuleComponent>
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    private const string DefaultWinMsg = "Govfor victory: Required percentage of CLF eliminated.";

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
        if (_gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!IsActiveRuleAndCLF(ev.Target) || ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private bool IsInArrestArea(EntityUid uid)
        => _area.TryGetArea(uid, out Entity<AreaComponent>? area, out _)
            && area.Value.Comp.CountAsArrestedForEndConditions;

    private void OnJumpsuitChanged(EntityUid wearer, string slot, EntityUid equipment)
    {
        if (slot != "jumpsuit" || Prototype(equipment)?.ID != "AU14CivilianPrisonJumpsuit")
            return;

        if (!IsActiveRuleAndCLF(wearer))
            return;

        CheckVictoryCondition();
    }

    private bool HasPrisonJumpsuit(EntityUid uid)
        => _inventory.TryGetSlotEntity(uid, "jumpsuit", out EntityUid? suit)
            && Prototype(suit!.Value)?.ID == "AU14CivilianPrisonJumpsuit";

    private bool IsActiveRuleAndCLF(EntityUid uid)
        => _gameTicker.IsGameRuleActive<KillAllClfRuleComponent>()
            && TryComp(uid, out NpcFactionMemberComponent? faction)
            && ThreatRuleHelper.HasFaction(faction, "clf");

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllClfRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllClfRuleComponent ruleComp, out _))
            return;

        int eliminated = 0, total = 0;
        int requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        bool countArrests = ruleComp.Arrest;
        bool crashedDropship = _threatRuleHelper.HasCrashedDropship();

        EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent> query = _entMan
            .EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState,
            out NpcFactionMemberComponent? faction))
        {
            if (!ThreatRuleHelper.HasFaction(faction, "clf"))
                continue;

            if (_threatRuleHelper.IsExcludedFromVictory(uid, mobState))
                continue;

            if (_threatRuleHelper.IsEvacuated(uid))
                continue;

            if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)) && mobState.CurrentState != MobState.Dead)
                continue;

            total++;

            if (mobState.CurrentState == MobState.Dead)
                eliminated++;
            else if (HasPrisonJumpsuit(uid)
                || (countArrests && ((TryComp(uid, out CuffableComponent? cuffable)
                        && cuffable.CuffedHandCount > 0)
                    || IsInArrestArea(uid))))
                eliminated++;
        }

        if (total == 0)
            return;
        if (!ThreatRuleHelper.MeetsRequiredPercent(eliminated, total, requiredPercent))
            return;
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        string? winMessage = !string.IsNullOrEmpty(ruleComp.WinMessage)
            ? ruleComp.WinMessage
            : _auRoundSystem.SelectedThreat?.WinMessage;
        _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage) ? winMessage : DefaultWinMsg);
    }
}
