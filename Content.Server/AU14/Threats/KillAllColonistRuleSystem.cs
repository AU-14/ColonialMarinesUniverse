using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.AU14;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.SSDIndicator;

namespace Content.Server.AU14.Threats;

public sealed class KillAllColonistRuleSystem : GameRuleSystem<KillAllColonistRuleComponent>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly Round.AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;

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

    private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
    {
        if (_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllColonist rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        // Get the active rule entity and its component to read Percent
        var queryRule = EntityQueryEnumerator<KillAllColonistRuleComponent, GameRuleComponent>();
        if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
            return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        var crashedDropship = HasCrashedDropship();

        // Count total and dead AUColonist mobs (excluding evacuated)
        var total = 0;
        var dead = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist"))
            {
                if (IsExcludedFromKillCount(uid))
                    continue;

                if (crashedDropship && TryComp(uid, out TransformComponent? xform) && _rmcPlanet.IsOnPlanet(xform))
                    continue;

                // If the entity's grid was evacuated, count them as dead (do not skip)
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
        }

        if (total == 0)
            return; // nothing to count

        var percentDead = (int) ((double)dead / total * 100.0);

        if (percentDead >= requiredPercent)
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            // End round, threat wins. Prefer configured win message.
            var winMessage = _auRoundSystem._selectedthreat.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
            {
                _gameTicker.EndRound(winMessage);
            }
            else
            {
                _gameTicker.EndRound("Threat victory: Required percentage of Colonists eliminated.");
            }
        }
    }
}
