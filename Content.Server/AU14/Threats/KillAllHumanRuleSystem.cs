using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.AU14;
using Content.Shared.Cuffs.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.SSDIndicator;

namespace Content.Server.AU14.Threats;

/// <summary>
/// Kill-all rule that targets all humanoid mobs (any entity with HumanoidAppearanceComponent),
/// excluding xenos. Evacuated entities are excluded from the count entirely.
/// </summary>
public sealed partial class KillAllHumanRuleSystem : GameRuleSystem<KillAllHumanRuleComponent>
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private Round.AuRoundSystem _auRoundSystem = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;

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
        if (_gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>())
            return;

        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    /// <summary>
    /// Called by KillAllRulesHandcuffSystem when a human entity is handcuffed.
    /// </summary>
    public void OnHandcuffEvent(EntityUid uid)
    {
        CheckVictoryCondition();
    }

    private bool IsInArrestArea(EntityUid uid)
    {
        return _area.TryGetArea(uid, out var area, out _) && area.Value.Comp.CountAsArrestedForEndConditions;
    }

    private bool IsExcludedFromKillCount(EntityUid uid)
    {
        return (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD) ||
               HasComp<XenoNestedComponent>(uid);
    }

    private bool HasCrashedDropship()
    {
        var dropships = EntityQueryEnumerator<DropshipComponent>();
        while (dropships.MoveNext(out _, out var dropship))
        {
            if (dropship.Crashed)
                return true;
        }

        return false;
    }

    private void CheckVictoryCondition()
    {
        var queryRule = EntityQueryEnumerator<KillAllHumanRuleComponent, GameRuleComponent>();
        if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
            return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        var countArrests = ruleComp.Arrest;
        var crashedDropship = HasCrashedDropship();

        // Count all humanoid mobs (excluding xenos and evacuated)
        var total = 0;
        var eliminated = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var mobState, out _))
        {
            // Xenos with humanoid appearance (e.g. cultists in human form) are still humanoid — include them.
            // But actual xenos (XenoComponent) are not humans.
            if (_entityManager.HasComponent<XenoComponent>(uid))
                continue;

            if (IsExcludedFromKillCount(uid))
                continue;

            if (crashedDropship && TryComp(uid, out TransformComponent? xform) && _rmcPlanet.IsOnPlanet(xform))
                continue;

            // If the entity's grid has been evacuated, count them as dead (do not skip)
            if (IsEvacuated(uid))
            {
                total++;
                eliminated++;
                continue;
            }

            total++;

            if (mobState.CurrentState == MobState.Dead)
            {
                eliminated++;
            }
            else if (countArrests &&
                     ((_entityManager.TryGetComponent(uid, out CuffableComponent? cuffable) && cuffable.CuffedHandCount > 0) ||
                      IsInArrestArea(uid)))
            {
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

            var customMessage = ruleComp.WinMessage;
            if (!string.IsNullOrEmpty(customMessage))
            {
                _gameTicker.EndRound(customMessage);
            }
            else
            {
                var winMessage = _auRoundSystem._selectedthreat.WinMessage;
                if (!string.IsNullOrEmpty(winMessage))
                {
                    _gameTicker.EndRound(winMessage);
                }
                else
                {
                    _gameTicker.EndRound("Threat victory: Required percentage of humans eliminated.");
                }
            }
        }
    }
}

