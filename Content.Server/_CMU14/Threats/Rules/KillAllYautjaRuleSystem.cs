using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Evacuation;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using KillAllYautjaRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllYautjaRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class KillAllYautjaRuleSystem : GameRuleSystem<KillAllYautjaRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;
    private const string DefaultWinMsg = "The Bad Blood Clan has been eliminated.";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllYautjaRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllYautjaRuleComponent>())
            return;
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        EntityQueryEnumerator<ActiveGameRuleComponent, KillAllYautjaRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out KillAllYautjaRuleComponent ruleComp, out _))
            return;

        int requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        int eliminated = 0, total = 0;

        EntityQueryEnumerator<MobStateComponent, YautjaComponent> query = _entMan
            .EntityQueryEnumerator<MobStateComponent, YautjaComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState, out _))
        {
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
