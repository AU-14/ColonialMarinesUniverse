using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Evacuation;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationMimicTransformedComponent
    = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicTransformedComponent;
using KillAllAbominationsRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllAbominationsRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Counts every abomination in the world — natural-form castes via
///     AbominationComponent and disguised mimics via
///     AbominationMimicTransformedComponent — and ends the round when the
///     configured percentage are dead. Mimic parents parked on the polymorph
///     paused map are skipped (the disguise on top is the live one); without
///     that filter the rule would double-count every disguised player.
/// </summary>
public sealed partial class KillAllAbominationsRuleSystem : GameRuleSystem<KillAllAbominationsRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    private const string DefaultWinMsg = "The Threat has been Eliminated";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllAbominationsRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllAbominationsRuleComponent>())
            return;
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllAbominationsRuleComponent, GameRuleComponent>
            queryRule = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllAbominationsRuleComponent ruleComp, out _))
            return;

        int requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        int eliminated = 0, total = 0;

        EntityQueryEnumerator<MobStateComponent> query = _entMan.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState))
        {
            bool isAbom = _entMan.HasComponent<AbominationComponent>(uid)
                || _entMan.HasComponent<AbominationMimicTransformedComponent>(uid);
            if (!isAbom)
                continue;

            // skip parents, counting both would double up
            if (_entMan.TryGetComponent(uid, out MetaDataComponent? meta) && meta.EntityPaused)
                continue;

            total++;
            if (mobState.CurrentState == MobState.Dead || _threatRuleHelper.IsEvacuated(uid))
                eliminated++;
        }

        if (total == 0)
            return;
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;
        if (!ThreatRuleHelper.MeetsRequiredPercent(eliminated, total, requiredPercent))
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage) ? winMessage : DefaultWinMsg);
    }
}
