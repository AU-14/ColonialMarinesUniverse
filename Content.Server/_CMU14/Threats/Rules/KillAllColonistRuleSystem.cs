using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.ColonyEvacuation;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using KillAllColonistRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllColonistRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Kill-all rule that targets all Colonists, excludes SSD.
///     Colonists wearing a prisoner jumpsuit, or handcuffed, or inside brig, or dead are eliminated.
/// </summary>
public sealed partial class KillAllColonistRuleSystem : GameRuleSystem<KillAllColonistRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    private const string DefaultWinMsg = "Threat victory: Required percentage of Colonists eliminated.";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

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
        => _gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>()
            && TryComp(uid, out NpcFactionMemberComponent? faction)
            && ThreatRuleHelper.HasFaction(faction, "aucolonist");

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllColonistRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllColonistRuleComponent ruleComp, out _))
            return;

        int requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        bool crashedDropship = _threatRuleHelper.HasCrashedDropship();
        int eliminated = 0, total = 0;

        EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent> query = _entMan
            .EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState,
            out NpcFactionMemberComponent? faction))
        {
            if (!ThreatRuleHelper.HasFaction(faction, "aucolonist"))
                continue;

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
        }

        if (total == 0)
            return;

        if (!ruleComp.ColonyEvacTriggered
            && ruleComp.ColonyEvacThreshold > 0
            && ThreatRuleHelper.MeetsRequiredPercent(eliminated, total, ruleComp.ColonyEvacThreshold))
        {
            ruleComp.ColonyEvacTriggered = true;
            var evacEv = new ColonyWithdrawEvacEnabledEvent();
            RaiseLocalEvent(ref evacEv);
        }

        if (!ThreatRuleHelper.MeetsRequiredPercent(eliminated, total, requiredPercent))
            return;
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage) ? winMessage : DefaultWinMsg);
    }
}
