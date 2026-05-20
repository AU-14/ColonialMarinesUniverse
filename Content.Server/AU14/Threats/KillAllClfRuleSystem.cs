using System.Linq;
using Content.Shared.Cuffs.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.SSDIndicator;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.AU14;

namespace Content.Server.AU14.Threats;

public sealed partial class KillAllClfRuleSystem : GameRuleSystem<KillAllClfRuleComponent>
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private Round.AuRoundSystem _auRoundSystem = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private InventorySystem _inventory = default!;

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
        if (_gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
            CheckVictoryCondition();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllClf rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        CheckVictoryCondition();
    }

    /// <summary>
    /// Called by KillAllRulesHandcuffSystem when a CLF entity is handcuffed.
    /// </summary>
    public void OnHandcuffEvent(EntityUid uid)
    {
        CheckVictoryCondition();
    }

    private bool HasPrisonJumpsuit(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "jumpsuit", out var suit)
            && Prototype(suit!.Value)?.ID == "AU14CivilianPrisonJumpsuit";
    }

    private bool IsInArrestArea(EntityUid uid)
    {
        return _area.TryGetArea(uid, out var area, out _) && area.Value.Comp.CountAsArrestedForEndConditions;
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

    private bool IsExcludedFromKillCount(EntityUid uid)
    {
        return (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD) ||
               HasComp<XenoNestedComponent>(uid);
    }

    private void CheckVictoryCondition()
    {
        // Get the active rule entity and its component to read Percent
        var queryRule = EntityQueryEnumerator<KillAllClfRuleComponent, GameRuleComponent>();
        if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
            return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);
        var countArrests = ruleComp.Arrest;
        var crashedDropship = HasCrashedDropship();

        // Count total and dead/arrested CLF mobs (excluding evacuated)
        var total = 0;
        var eliminated = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "clf"))
            {
                if (IsExcludedFromKillCount(uid))
                    continue;

                if (crashedDropship && _rmcPlanet.IsOnPlanet(Transform(uid)))
                    continue;

                // Skip evacuated entities entirely
                if (IsEvacuated(uid))
                    continue;

                total++;

                if (mobState.CurrentState == MobState.Dead)
                {
                    eliminated++;
                }
                // Wearing jumpsuit, or arrested flag is set and they're cuffed, or in the mapped brig areas
                else if (HasPrisonJumpsuit(uid)
                    || countArrests && ((TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
                    || IsInArrestArea(uid)))
                {
                    eliminated++;
                }
            }
        }

        if (total == 0)
            return; // nothing to count

        var percentEliminated = (int) ((double)eliminated / total * 100.0);

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
                    _gameTicker.EndRound("Govfor victory: Required percentage of CLF eliminated.");
                }
            }
        }
    }
}

