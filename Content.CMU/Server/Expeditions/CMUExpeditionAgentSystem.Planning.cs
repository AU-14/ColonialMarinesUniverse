using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using F = Content.Shared.CMU14.Expeditions.CMUTacticalFact;
using A = Content.Shared.CMU14.Expeditions.CMUTacticalAction;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool RunPlan(EntityUid uid, CMUExpeditionAgentComponent agent, bool armed, float damage, bool hit, TimeSpan now)
    {
        if (!agent.PlanningEnabled || agent.Action == null && !OptionalDecisionReady(agent))
            return false;
        if (agent.Action == null && now < agent.MedicalCoverUntil && armed && !hit && damage < agent.RetreatDamage)
            return false;
        if (agent.Action != null)
        {
            if (hit || now >= agent.ActionUntil || !ManeuverSupported(uid, agent, now) ||
                GrenadeDanger(Transform(uid).Coordinates) && agent.Action != A.TakeCover)
            {
                CancelPlan(uid, agent, true);
                return false;
            }
            if (ContinueAction(uid, agent, damage, now))
                return true;
        }
        if (agent.Plan.Count > 0)
            return StartNextAction(uid, agent, damage, now);
        if (armed && now < agent.NextPlan)
            return false;
        var spare = SpareAmmunition(uid);
        // An empty rifle must not wait for the optional tactical-planning cadence.
        if (now < agent.NextPlan && (armed || spare == null))
            return false;
        agent.NextPlan = now + TimeSpan.FromSeconds(1);
        var safe = TreatmentSafe(uid, agent);
        var medicine = HasMedicine(uid);
        var mustHeal = medicine && damage >= agent.RetreatDamage && ShouldTreat(agent, damage, now);
        var goal = !armed && spare != null ? CMUTacticalGoal.Rearm : mustHeal ? CMUTacticalGoal.Recover : CMUTacticalGoal.Fight;

        // Optional squad work starts between attacks, never in the middle of a peek/withdrawal.
        var available = agent.State is CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Recover or CMUExpeditionAgentState.Watch or
            CMUExpeditionAgentState.OutOfAmmo ||
            agent.State == CMUExpeditionAgentState.HoldAngle && now >= agent.PositionCommittedUntil;
        agent.Casualty = null;
        agent.ActionDestination = null;
        agent.GrenadeTarget = null;
        agent.SmokeGrenade = false;
        if (goal == CMUTacticalGoal.Fight)
        {
            if ((armed || safe) && damage < agent.RetreatDamage && available && now >= agent.NextRescue && agent.Stress < 0.65f &&
                (agent.HasCoveringAlly || agent.VisibleThreats.Count == 0))
                agent.Casualty = FindCasualty(uid, agent);
            if (agent.Casualty != null)
                goal = CMUTacticalGoal.Rescue;
            else if ((available || agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage) &&
                now >= agent.NextGrenade && GrenadeDecisionAvailable(uid, agent, now) &&
                Grenade(uid, false) is { } grenade && BlastPoint(uid, agent, grenade) is { } target)
            {
                agent.GrenadeTarget = target;
                agent.SmokeGrenade = false;
                agent.GrenadeDecision = "cluster-or-last-resort";
                goal = CMUTacticalGoal.Flush;
            }
            else if (agent.RecoveryUntil <= now && agent.Duty is not (CMUSquadDuty.Overwatch or CMUSquadDuty.RearGuard or CMUSquadDuty.Medic or CMUSquadDuty.Recover) &&
                damage < agent.RetreatDamage && available && armed && !agent.Crossfire && now >= agent.NextFlank && agent.HasCoveringAlly &&
                (agent.Duty == CMUSquadDuty.Advance || agent.Initiative >= (agent.CombatRole == CMUExpeditionCombatRole.Flanker ? 0.4f : 0.6f) * agent.LearnedFlankCost ||
                    agent.RepeatedPeekHits >= 2) && !SquadHasFlanker(uid, agent) &&
                FlankPosition(uid, agent) is { } flank && TryReserveManeuver(uid, agent, now))
            {
                agent.ActionDestination = flank;
                goal = CMUTacticalGoal.Flank;
            }
        }

        // Spend smoke on a threatened withdrawal/reload, crossfire, or an exposed rescue.
        // Throwing it never fabricates shelter: live sight and cover checks still decide the next action.
        if (!safe && agent.GrenadeTarget == null && agent.RushTarget == null &&
            (goal is CMUTacticalGoal.Rearm or CMUTacticalGoal.Recover ||
             goal == CMUTacticalGoal.Fight && (!armed || agent.Crossfire && agent.Stress >= 0.6f && agent.HasCoveringAlly)) &&
            now >= agent.NextGrenade && GrenadeDecisionAvailable(uid, agent, now) &&
            Grenade(uid, true) is { } screen && SmokePoint(uid, agent, screen, Transform(uid).Coordinates) is { } screening)
        {
            agent.GrenadeTarget = screening;
            agent.SmokeGrenade = true;
            agent.GrenadeDecision = "screen-withdrawal";
            goal = CMUTacticalGoal.Flush;
        }

        if (goal == CMUTacticalGoal.Rescue && agent.Casualty is { } casualty &&
            !ShelteredFromKnownThreats(uid, agent, Transform(casualty).Coordinates) && now >= agent.NextGrenade &&
            GrenadeDecisionAvailable(uid, agent, now) && Grenade(uid, true) is { } smoke &&
            SmokePoint(uid, agent, smoke, Transform(casualty).Coordinates) is { } rescueScreen)
        {
            agent.GrenadeTarget = rescueScreen;
            agent.SmokeGrenade = true;
            agent.GrenadeDecision = "screen-rescue";
        }

        EntityCoordinates? shelter = null;
        if ((!safe && goal is CMUTacticalGoal.Rearm or CMUTacticalGoal.Recover) || goal == CMUTacticalGoal.Rescue)
            shelter = FindPosition(uid, agent, Transform(uid), true)?.Anchor;
        // No hard shelter: a stationary reload can use a reserved shooter, with immediate
        // interruption on damage, a rush or lost coverage. Do not make every empty guard wait forever.
        if (goal == CMUTacticalGoal.Rearm && !safe && shelter == null &&
            ReloadPressureSafe(uid, agent) && TryReserveManeuver(uid, agent, now))
            safe = agent.CoveringShooter != null;
        if (goal == CMUTacticalGoal.Rescue && shelter is { } refuge && agent.LastSeen is { } threat)
        {
            var away = refuge.Position - _transform.ToCoordinates(refuge.EntityId, _transform.ToMapCoordinates(threat)).Position;
            if (away.LengthSquared() > 0.01f)
            {
                var deep = refuge.Offset(Vector2.Normalize(away) * 1.5f);
                if (TraversablePassage(uid, refuge, deep) &&
                    ClearLane(uid, refuge, deep, 0.4f, movement: true) && ShelteredFromKnownThreats(uid, agent, deep))
                    shelter = deep;
            }
        }
        // A quiet casualty can be brought to the rescuer's current safe position.
        if (goal == CMUTacticalGoal.Rescue && shelter == null && safe)
            shelter = Transform(uid).Coordinates;
        agent.RescueShelter = shelter;
        if (goal is CMUTacticalGoal.Rearm or CMUTacticalGoal.Recover)
            agent.ActionDestination = shelter;

        var facts = F.None;
        if (armed) facts |= F.Armed;
        if (safe) facts |= F.Safe;
        if (spare != null) facts |= F.SpareAmmo;
        if (medicine) facts |= F.Medicine;
        if (damage >= agent.HealDamage && ShouldTreat(agent, damage, now)) facts |= F.Wounded;
        if (shelter != null) facts |= F.CoverAvailable;
        if (agent.LastSeen != null && now < agent.ForgetAt) facts |= F.Contact;
        if (agent.Casualty != null) facts |= F.Casualty;
        if (agent.GrenadeTarget != null) facts |= F.Grenade | F.ThrowLane;
        if (goal == CMUTacticalGoal.Flank) facts |= F.FlankAvailable;
        var operators = new List<CMUTacticalOperator>
        {
            new(A.TakeCover, F.CoverAvailable, F.None, F.Safe, F.None, 2),
            new(A.Reload, F.Safe | F.SpareAmmo, F.None, F.Armed, F.SpareAmmo, 3),
            new(A.Treat, F.Safe | F.Medicine | F.Wounded, F.None, F.None, F.Wounded, 3),
            new(A.ApproachCasualty, F.Casualty | (agent.SmokeGrenade && agent.GrenadeTarget != null ? F.PressureApplied : F.None),
                F.None, F.AtCasualty, F.Safe, 2),
            new(A.GrabCasualty, F.AtCasualty, F.None, F.Dragging, F.None, 1),
            new(A.DragCasualty, F.Dragging | F.CoverAvailable, F.None, F.Safe | F.Rescued, F.Dragging, 4),
            new(A.ThrowGrenade, F.Grenade | F.ThrowLane, F.None, F.PressureApplied, F.Grenade, 1),
            new(A.Flank, F.Armed | F.FlankAvailable, F.None, F.Flanked, F.Safe, 3 * agent.LearnedFlankCost),
            new(A.Attack, F.Armed | F.Contact, F.None, F.Engaged, F.None, 1),
        };
        operators.RemoveAll(op => agent.FailedActions.TryGetValue(op.Action, out var until) && now < until);
        var required = goal switch
        {
            CMUTacticalGoal.Rearm => F.Armed,
            CMUTacticalGoal.Recover => F.Safe,
            CMUTacticalGoal.Rescue => F.Rescued,
            CMUTacticalGoal.Flush => F.PressureApplied,
            CMUTacticalGoal.Flank => F.Flanked,
            _ => F.Engaged,
        };
        var plan = CMUTacticalPlanner.Plan(facts, required, goal == CMUTacticalGoal.Recover ? F.Wounded : F.None,
            operators, out agent.PlannerExpanded);
        agent.Goal = goal;
        if (plan == null || plan.Count == 0)
        {
            ReleaseManeuver(uid, agent);
            agent.Casualty = null;
            return false;
        }
        foreach (var action in plan)
            agent.Plan.Enqueue(action);
        return StartNextAction(uid, agent, damage, now);
    }

    private bool StartNextAction(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        ClearScavenging(uid, agent);
        var action = agent.Plan.Dequeue();
        agent.Action = action;
        agent.ActionStarted = now;
        agent.ActionUntil = now + TimeSpan.FromSeconds(10);
        agent.ActionInitialDamage = damage;
        agent.ActionComplete = false;
        var success = false;
        switch (action)
        {
            case A.Attack:
                agent.Action = null;
                return false; // The combat executor owns aiming, burst limits and every individual shot.
            case A.TakeCover:
            case A.Flank:
                if (agent.ActionDestination is { } destination &&
                    (action != A.TakeCover || agent.Goal != CMUTacticalGoal.Recover ||
                        damage >= agent.EmergencyHealDamage || TryReserveManeuver(uid, agent, now)))
                    success = StartPlanMove(uid, agent, destination, now);
                break;
            case A.Reload:
                agent.State = CMUExpeditionAgentState.Reloading;
                agent.ActionItem = SpareAmmunition(uid);
                success = ReloadSafe(uid, agent) && agent.ActionItem != null;
                break;
            case A.Treat:
                success = TryTreat(uid, agent, damage, now);
                break;
            case A.ThrowGrenade:
                agent.State = CMUExpeditionAgentState.Throwing;
                agent.ActionItem = Grenade(uid, agent.SmokeGrenade);
                success = agent.GrenadeTarget is { } target && agent.ActionItem is { } grenade && SafeGrenade(uid, target, grenade) &&
                    ReserveGrenadeDecision(uid, agent, now);
                break;
            case A.ApproachCasualty:
                if (ValidCasualty(uid, agent) && TryReserveManeuver(uid, agent, now))
                    success = StartPlanMove(uid, agent, Transform(agent.Casualty!.Value).Coordinates, now);
                break;
            case A.GrabCasualty:
                if (_guns.TryGetGun(uid, out var gun))
                    _wield.TryUnwield(gun.Owner, uid);
                success = ValidCasualty(uid, agent) && _hands.GetEmptyHandCount(uid) > 0 &&
                    _transform.InRange(Transform(uid).Coordinates, Transform(agent.Casualty!.Value).Coordinates, 1.5f) &&
                    _pulling.TryStartPull(uid, agent.Casualty.Value);
                agent.State = CMUExpeditionAgentState.Rescuing;
                agent.ActionComplete = success;
                break;
            case A.DragCasualty:
                if (ValidCasualty(uid, agent) && agent.RescueShelter is { } refuge)
                    success = StartPlanMove(uid, agent, refuge, now);
                break;
        }
        if (success && action is A.Reload or A.ThrowGrenade && _guns.TryGetGun(uid, out var utilityGun))
        {
            _steering.Unregister(uid);
            _wield.TryUnwield(utilityGun.Owner, uid);
        }
        if (!success)
            CancelPlan(uid, agent, true);
        return success;
    }

    private bool StartPlanMove(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point, TimeSpan now)
    {
        ClearCover(agent);
        if (!BuildTacticalRoute(uid, agent, point))
            return false;
        agent.ActiveMoveDestination = point;
        BeginMove(uid, agent, point, CMUExpeditionAgentState.PlanMove, now);
        agent.MoveUntil = agent.ActionUntil;
        return true;
    }

    private bool ContinueAction(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        if (agent.Action is A.Reload or A.ThrowGrenade && agent.ActionDoAfter == null && !agent.ActionComplete)
        {
            // Wait for the real virtual-grip removal, not an additional reaction timer.
            if (_hands.GetEmptyHandCount(uid) == 0 && now - agent.ActionStarted < TimeSpan.FromSeconds(0.5))
                return true;
            var delay = 0.8;
            if (agent.Action == A.Reload)
                delay = _guns.TryGetGun(uid, out var reloading) &&
                    TryComp<Content.Shared.Weapons.Ranged.Components.BallisticAmmoProviderComponent>(reloading, out var tube)
                    ? Math.Max(agent.ShellReloadDuration.TotalSeconds, tube.InsertDelay) : agent.MagazineReloadDuration.TotalSeconds;
            if (agent.ActionItem is not { } item || !Exists(item) ||
                !StartUtility(uid, agent, item, TimeSpan.FromSeconds(delay)))
            {
                CancelPlan(uid, agent, true);
                return false;
            }
            return true;
        }
        if (agent.Action is A.ApproachCasualty or A.GrabCasualty or A.DragCasualty && !ValidCasualty(uid, agent))
        {
            CancelPlan(uid, agent, true);
            return false;
        }
        if (agent.State == CMUExpeditionAgentState.PlanMove)
        {
            var approaching = agent.Action == A.ApproachCasualty && agent.Casualty is { } patient &&
                _transform.InRange(Transform(uid).Coordinates, Transform(patient).Coordinates, 1.2f);
            if (!approaching && ContinueMove(uid, agent, Transform(uid), now))
                return true;
            _steering.Unregister(uid);
            if (!approaching && (agent.LastMoveFailed || agent.ActiveMoveDestination is not { } expected ||
                !_transform.InRange(Transform(uid).Coordinates, expected, 0.4f) ||
                agent.Action == A.TakeCover && !TreatmentSafe(uid, agent)))
            {
                CancelPlan(uid, agent, true);
                return false;
            }
            if (agent.Action == A.DragCasualty)
            {
                if (agent.Casualty is not { } casualty || !TryComp<PullableComponent>(casualty, out var pull) || pull.Puller != uid ||
                    !ShelteredFromKnownThreats(uid, agent, Transform(casualty).Coordinates))
                {
                    CancelPlan(uid, agent, true);
                    return false;
                }
                ReleaseCasualty(uid, agent);
                agent.Rescues++;
                agent.NextRescue = now + TimeSpan.FromSeconds(20);
            }
            if (agent.Action == A.Flank)
            {
                agent.Flanks++;
                agent.NextFlank = now + TimeSpan.FromSeconds(18);
                agent.NextReposition = now + TimeSpan.FromSeconds(3);
                RecordTactic(uid, agent, damage <= agent.ActionInitialDamage);
            }
            agent.ActionComplete = true;
        }
        if (agent.Action == A.Treat && agent.State != CMUExpeditionAgentState.Healing && damage < agent.ActionInitialDamage)
            agent.ActionComplete = true;
        if (!agent.ActionComplete)
            return true;
        ReleaseManeuver(uid, agent);
        agent.Route.Clear();
        agent.RouteDestination = null;
        agent.CoverDestination = null;
        agent.Action = null;
        agent.State = CMUExpeditionAgentState.Guard;
        if (agent.Plan.Count == 0)
            agent.Casualty = null;
        return false;
    }

    private void CancelPlan(EntityUid uid, CMUExpeditionAgentComponent agent, bool failed)
    {
        ClearScavenging(uid, agent);
        ReleaseManeuver(uid, agent);
        CancelMedical(uid, agent, "task-cancelled", failed);
        if (failed && agent.Action is { } action)
        {
            agent.FailedActions[action] = _timing.CurTime + TimeSpan.FromSeconds(action == A.Reload ? 0.75 : 8);
            agent.FailedPlans++;
            if (action == A.Flank)
                RecordTactic(uid, agent, false);
        }
        var doAfter = agent.ActionDoAfter;
        agent.ActionDoAfter = null;
        if (doAfter is { } id && _doAfter.GetStatus(id) == DoAfterStatus.Running)
            _doAfter.Cancel(id);
        if (agent.ActionItem is { } item && Exists(item) && Supplies(uid, out var supplies))
            _hands.TryDropIntoContainer(uid, item, supplies.Container);
        ReleaseCasualty(uid, agent);
        agent.Action = null;
        agent.ActionItem = null;
        agent.GrenadeReservationUntil = TimeSpan.Zero;
        agent.Casualty = null;
        agent.Plan.Clear();
        agent.Route.Clear();
        agent.RouteDestination = null;
        agent.NextPlan = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        ClearCover(agent);
        _steering.Unregister(uid);
        agent.State = CMUExpeditionAgentState.Guard;
    }

    private EntityUid? FindCasualty(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !_mobs.IsCritical(other) || HasComp<ActorComponent>(other) ||
                HasComp<CMUExpeditionPatientComponent>(other) ||
                !SameSquad(uid, agent, other, buddy) || !Visible(uid, other, 10) ||
                TryComp<PullableComponent>(other, out var pulled) && pulled.Puller != null)
                continue;
            var claims = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
            var reserved = false;
            while (claims.MoveNext(out var rescuer, out var claim))
                reserved |= rescuer != uid && claim.Casualty == other && claim.Action != null;
            if (!reserved)
                return other;
        }
        return null;
    }

    private bool ValidCasualty(EntityUid uid, CMUExpeditionAgentComponent agent) => agent.Casualty is { } casualty &&
        Exists(casualty) && _mobs.IsCritical(casualty) && !HasComp<ActorComponent>(casualty) &&
        !HasComp<CMUExpeditionPatientComponent>(casualty) &&
        IsFriendly(uid, casualty) && Transform(uid).MapID == Transform(casualty).MapID;

    private bool SquadHasFlanker(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other != uid && SameSquad(uid, agent, other, buddy) && buddy.Action == A.Flank)
                return true;
        }
        return false;
    }
}
