using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.RoundStatistics;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.CMU14.Threats.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.CMU14.Threats.Rules;

/// <summary>
/// The colony wins against a Working Joes shipment once enough threat units (<see cref="CMUJoeThreatUnitComponent"/>)
/// are dead, deleted or off the planet; a unit left aboard the departed ship counts as eliminated. Unlike the other
/// kill rules this one does not drop synthetics: every unit is one. A cured unit leaves the count. Re-checked on a
/// unit's death, on a unit leaving (owner of its <c>ComponentRemove</c>), and every 5 s, because gibs and deletes
/// never pass through Dead.
/// </summary>
public sealed partial class CMUKillAllHijackedJoesRuleSystem : GameRuleSystem<CMUKillAllHijackedJoesRuleComponent>
{
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private CMURoundStatisticsSystem _roundStats = default!;

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUJoeThreatUnitComponent, MobStateChangedEvent>(OnUnitMobStateChanged);
        SubscribeLocalEvent<CMUJoeThreatUnitComponent, ComponentRemove>(OnUnitRemove);
    }

    private void OnUnitMobStateChanged(Entity<CMUJoeThreatUnitComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            CheckVictoryCondition();
    }

    /// <summary>
    /// A cure removes the unit from the total; a deleted or gibbed unit stays counted, as eliminated.
    /// </summary>
    private void OnUnitRemove(Entity<CMUJoeThreatUnitComponent> ent, ref ComponentRemove args)
    {
        if (!GameTicker.IsGameRuleActive<CMUKillAllHijackedJoesRuleComponent>())
            return;

        if (!TerminatingOrDeleted(ent))
        {
            var rules = QueryActiveRules();
            while (rules.MoveNext(out _, out _, out var rule, out _))
            {
                rule.Units.Remove(ent);
            }
        }

        CheckVictoryCondition();
    }

    protected override void ActiveTick(EntityUid uid, CMUKillAllHijackedJoesRuleComponent component,
        GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (Timing.CurTime < _nextCheck)
            return;

        _nextCheck = Timing.CurTime + CheckEvery;
        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out _, out var rule, out _))
        {
            var units = EntityQueryEnumerator<CMUJoeThreatUnitComponent>();
            while (units.MoveNext(out var uid, out var unit))
            {
                // a unit being cured is still here while its removal runs
                if (unit.LifeStage >= ComponentLifeStage.Stopping && !TerminatingOrDeleted(uid))
                    continue;

                rule.Units.Add(uid);
            }

            if (rule.Units.Count > 0)
                rule.HadUnits = true;
            else if (!rule.HadUnits)
                continue;

            var eliminated = 0;
            foreach (var uid in rule.Units)
            {
                if (IsEliminated(uid))
                    eliminated++;
            }

            var required = Math.Clamp(rule.Percent, 1, 100);
            if (!ThreatRuleHelper.MeetsRequiredPercent(eliminated, rule.Units.Count, required))
                continue;

            _roundStats.RecordThreatDefeatedRule("CMUKillAllHijackedJoesRule");
            GameTicker.EndRound(Loc.GetString("cmu-working-joes-colony-wins"));
            return;
        }
    }

    /// <summary>
    /// Dead, deleted, or not on the planet (the ship left with it, or it never got off). The planet's underground
    /// and elevated levels count as the planet.
    /// </summary>
    public bool IsEliminated(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return true;

        if (TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Dead)
            return true;

        var xform = Transform(uid);
        if (_rmcPlanet.IsOnPlanetLevel(xform))
            return false;

        // a ship flying between landing zones is coming back; only one parked off the planet, or flying home,
        // takes its passengers out of the round
        return !IsInTransitToPlanet(xform.GridUid);
    }

    private bool IsInTransitToPlanet(EntityUid? grid)
    {
        if (grid is not { } ship
            || !TryComp(ship, out FTLComponent? ftl)
            || ftl.State is not (FTLState.Starting or FTLState.Travelling or FTLState.Arriving))
        {
            return false;
        }

        return TryComp(ship, out DropshipComponent? dropship)
            && dropship.Destination is { } destination
            && !HasComp<ThirdPartyDropshipReturnDestinationComponent>(destination);
    }
}
