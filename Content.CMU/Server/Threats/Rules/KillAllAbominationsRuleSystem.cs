using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Evacuation;
using Content.Shared.AU14;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

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
    [Dependency] private IEntityManager _entityManager = default!;

    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;
    [Dependency] private GameTicker _gameTicker = default!;
    private EntityQuery<MetaDataComponent> _metaQuery;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        _metaQuery      = GetEntityQuery<MetaDataComponent>();
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

        if (!queryRule.MoveNext(out _, out _, out KillAllAbominationsRuleComponent? ruleComp, out _))
            return;

        int requiredPercent = Math.Clamp(ruleComp!.Percent, 1, 100);

        var total = 0;
        var dead  = 0;

        EntityQueryEnumerator<MobStateComponent> query = _entityManager.EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out EntityUid uid, out MobStateComponent? mobState))
        {
            // Natural-form abomination, OR a mimic currently wearing a face.
            bool isAbom = _entityManager.HasComponent<AbominationComponent>(uid)
             || _entityManager.HasComponent<AbominationMimicTransformedComponent>(uid);

            if (!isAbom)
                continue;

            // Skip parked polymorph parents — the disguise that points to
            // them is the "live" count, so counting both would double up.
            if (_metaQuery.TryGetComponent(uid, out MetaDataComponent? meta) && meta.EntityPaused)
                continue;

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

        if (percentDead < requiredPercent)
            return;

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        if (!string.IsNullOrEmpty(winMessage))
            _gameTicker.EndRound(winMessage);
        else
            _gameTicker.EndRound("The Threat has been Eliminated");
    }
}