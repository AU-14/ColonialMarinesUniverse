using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._CMU14.Yautja;
using Content.Shared.AU14;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._RMC14.Evacuation;

namespace Content.Server.AU14.Threats;

public sealed partial class KillAllYautjaRuleSystem : GameRuleSystem<KillAllYautjaRuleComponent>
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private Round.AuRoundSystem _auRoundSystem = default!;

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
        var xform = Transform(uid);
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
        var queryRule = QueryActiveRules();
        if (!queryRule.MoveNext(out _, out _, out var ruleComp, out _))
            return;

        var requiredPercent = Math.Clamp(ruleComp!.Percent, 1, 100);

        var total = 0;
        var dead = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, YautjaComponent>();
        while (query.MoveNext(out var uid, out var mobState, out _))
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

            var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
            _gameTicker.EndRound(!string.IsNullOrEmpty(winMessage)
                ? winMessage
                : "The Bad Blood Clan has been eliminated.");
        }
    }
}
