using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Evacuation;
using Content.Shared.AU14;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class KillAllYautjaRuleSystem : GameRuleSystem<KillAllYautjaRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
    }

    private bool IsEvacuated(EntityUid uid)
    {
        TransformComponent xform = Transform(uid);

        return xform.GridUid is { } grid && _evacuatedQuery.HasComp(grid);
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

        if (!queryRule.MoveNext(out _, out _, out KillAllYautjaRuleComponent? ruleComp, out _))
            return;

        int requiredPercent = Math.Clamp(ruleComp!.Percent, 1, 100);

        var total = 0;
        var dead  = 0;

        EntityQueryEnumerator<MobStateComponent, YautjaComponent> query = _entityManager
            .EntityQueryEnumerator<MobStateComponent, YautjaComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState, out _))
        {
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

        if (total == 0)
            return;

        var percentDead = (int)((double)dead / total * 100.0);

        if (percentDead >= requiredPercent)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
            _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage)
                ? winMessage
                : "The Bad Blood Clan has been eliminated.");
        }
    }
}