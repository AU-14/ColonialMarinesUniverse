using System.Linq;
using System.Diagnostics;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private sealed class SquadPlan
    {
        public readonly List<EntityUid> Members = new();
        public readonly Dictionary<EntityUid, (EntityCoordinates Point, TimeSpan Until)> Supplies = new();
        public string Phase = "holding";
        public TimeSpan Until;
        public EntityCoordinates? Rally;
        public EntityCoordinates? Contact;
        public EntityCoordinates? CasualtyPoint;
        public EntityUid? Runner;
        public EntityUid? Leader;
        public EntityUid? UrgentThreat;
    }

    private readonly Dictionary<EntityUid, SquadPlan> _squadPlans = new();
    private TimeSpan _nextSquadPlan;
    private double _squadPlanMilliseconds;

    private void UpdateSquadPlans(TimeSpan now)
    {
        if (now < _nextSquadPlan)
            return;
        _nextSquadPlan = now + TimeSpan.FromSeconds(1);
        var started = Stopwatch.GetTimestamp();
        foreach (var plan in _squadPlans.Values)
            plan.Members.Clear();
        var roots = new Dictionary<(MapId Map, int Squad), EntityUid>();
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (agent.Squad == 0 || transform.MapUid == null)
                continue;
            if (agent.SquadRoot is { } previousRoot && !Exists(previousRoot))
                agent.SquadRoot = null;
            if (agent.SquadRoot == null)
            {
                var key = (transform.MapID, agent.Squad);
                if (!roots.TryGetValue(key, out var root))
                    roots[key] = root = uid;
                agent.SquadRoot = root;
            }
            if (!_squadPlans.TryGetValue(agent.SquadRoot.Value, out var plan))
                _squadPlans[agent.SquadRoot.Value] = plan = new SquadPlan();
            plan.Members.Add(uid);
        }
        foreach (var (root, plan) in _squadPlans.ToArray())
        {
            if (plan.Members.Count == 0)
            {
                _squadPlans.Remove(root);
                continue;
            }
            plan.Members.Sort((a, b) => a.CompareTo(b));
            if (plan.Leader is not { } leader || !plan.Members.Contains(leader) || !CanOrderSquadMember(leader))
                plan.Leader = plan.Members.FirstOrDefault(member => CanOrderSquadMember(member) && !HasComp<CMUExpeditionMedicComponent>(member),
                    plan.Members.FirstOrDefault(CanOrderSquadMember, plan.Members[0]));
            RefreshSquadPlan(plan, now);
        }
        _squadPlanMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private void RefreshSquadPlan(SquadPlan plan, TimeSpan now)
    {
        var leader = plan.Leader!.Value;
        var anchor = Transform(leader).Coordinates;
        var local = plan.Members.Where(member => CanOrderSquadMember(member) && _transform.InRange(anchor, Transform(member).Coordinates, 22)).ToArray();
        if (local.Length == 0)
        {
            plan.Phase = "no-active-members";
            return;
        }
        var observers = local.Select(member => Comp<CMUExpeditionAgentComponent>(member)).ToArray();
        var contact = observers.FirstOrDefault(agent => agent.LastSeen != null && now < agent.ForgetAt);
        plan.Contact = contact?.LastSeen;
        var melee = observers.Any(agent => agent.RushTarget != null || agent.MeleeMemory.Count > 0);
        plan.UrgentThreat = local.Where(member => Comp<CMUExpeditionAgentComponent>(member).RushTarget != null)
            .OrderBy(member => MeleeClearance(Comp<CMUExpeditionAgentComponent>(member), Transform(member).Coordinates))
            .Select(member => Comp<CMUExpeditionAgentComponent>(member).RushTarget).FirstOrDefault();
        var casualties = observers.Count(agent => agent.LastDamage >= agent.RetreatDamage || agent.RecoveryUntil > now ||
            agent.Stress > .85f && agent.Courage < .6f) +
            plan.Members.Count(member => !_mobs.IsAlive(member) && _transform.InRange(anchor, Transform(member).Coordinates, 22));
        var low = local.Count(member => !_guns.TryGetGun(member, out var gun) || WeaponAmmo(gun) == 0 && SpareAmmunition(member) == null);
        var phase = contact == null ? (observers.Any(agent => agent.OrderedDestination != null || agent.TravelGoal != null) ? "travelling" : "holding") :
            melee ? "anti-rush" : casualties + low >= Math.Max(2, local.Length / 2) ? "withdraw" :
            observers.Any(agent => agent.AntiVehicle && agent.Target is { } target && ArmedVehicle(target)) ? "anti-armor" : "fire-and-move";
        // Emergencies override; ordinary plans persist long enough to execute their assignments.
        if (phase != plan.Phase && (now >= plan.Until || phase is "anti-rush" or "withdraw" || contact == null))
        {
            plan.Phase = phase;
            plan.Until = now + TimeSpan.FromSeconds(8);
            plan.Rally = anchor;
            foreach (var member in plan.Members)
                Comp<CMUExpeditionAgentComponent>(member).DutyUntil = TimeSpan.Zero;
        }
        plan.Rally ??= anchor;
        var direction = contact?.LastSeen is { } seen && Transform(seen.EntityId).MapID == Transform(leader).MapID
            ? _transform.ToMapCoordinates(seen).Position - _transform.GetWorldPosition(leader) : Vector2.UnitY;
        if (direction.LengthSquared() < .01f)
            direction = Vector2.UnitY;
        direction = Vector2.Normalize(direction);
        var side = new Vector2(-direction.Y, direction.X);
        var advancing = local.Any(member => Comp<CMUExpeditionAgentComponent>(member) is { Duty: CMUSquadDuty.Advance } existing &&
            now < existing.DutyUntil && existing.LastDamage < existing.RetreatDamage && existing.RecoveryUntil <= now);
        for (var index = 0; index < local.Length; index++)
        {
            var member = local[index];
            var agent = Comp<CMUExpeditionAgentComponent>(member);
            agent.SquadPhase = plan.Phase;
            if (now < agent.DutyUntil && agent.LastDamage < agent.RetreatDamage && agent.RecoveryUntil <= now)
            {
                advancing |= agent.Duty == CMUSquadDuty.Advance;
                continue;
            }
            var duty = agent.RecoveryUntil > now || agent.LastDamage >= agent.RetreatDamage ? CMUSquadDuty.Recover :
                HasComp<CMUExpeditionMedicComponent>(member) ? CMUSquadDuty.Medic :
                agent.AntiVehicle && plan.Phase == "anti-armor" ? CMUSquadDuty.AntiArmor :
                index == local.Length - 1 && local.Length > 3 ? CMUSquadDuty.RearGuard :
                !advancing && plan.Phase == "fire-and-move" && agent.CombatRole is not (CMUExpeditionCombatRole.Support or CMUExpeditionCombatRole.Marksman)
                    ? CMUSquadDuty.Advance : CMUSquadDuty.Overwatch;
            advancing |= duty == CMUSquadDuty.Advance;
            agent.Duty = duty;
            agent.DutyUntil = now + TimeSpan.FromSeconds(6);
            var offset = side * ((index - (local.Length - 1) * .5f) * 2.2f);
            offset -= direction * (duty is CMUSquadDuty.Medic or CMUSquadDuty.Recover or CMUSquadDuty.RearGuard ? 3 : plan.Phase is "anti-rush" or "withdraw" ? 2 : 0);
            var world = _transform.ToMapCoordinates(anchor).Offset(offset);
            var point = _transform.ToCoordinates(anchor.EntityId, world);
            if (TrySquadCoordinates(point, out point) && ValidOrderPoint(member, point))
                agent.DutyPoint = point;
            else
                agent.DutyPoint = null;
        }
        foreach (var source in plan.Supplies.Where(pair => pair.Value.Until <= now || !Exists(pair.Key)).Select(pair => pair.Key).ToArray())
            plan.Supplies.Remove(source);
        if (plan.Runner is { } runner && (!plan.Members.Contains(runner) ||
            Comp<CMUExpeditionAgentComponent>(runner) is { SupplySource: null, DeliveryRecipient: null }))
            plan.Runner = null;
    }

    private SquadPlan? PlanFor(CMUExpeditionAgentComponent agent) =>
        agent.SquadRoot is { } root && _squadPlans.TryGetValue(root, out var plan) ? plan : null;

    private bool FollowSquadDuty(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null || agent.FlareItem != null ||
            agent.RushTarget != null || CommittedMovement(agent) || HasCoverCommitment(uid, agent, now) ||
            !OptionalDecisionReady(agent) || now < agent.NextDutyMove || agent.DutyPoint is not { } point ||
            agent.Home is not { } home || !_transform.InRange(home, point, agent.LeashRange) ||
            _transform.InRange(Transform(uid).Coordinates, point, 1.1f) || !ValidOrderPoint(uid, point) || Reserved(uid, point))
            return false;
        // A plan can request movement, never invent visibility or authorize an unsafe shot.
        var reposition = agent.SquadPhase is "anti-rush" or "withdraw" || agent.Duty == CMUSquadDuty.Recover ||
            agent.Duty == CMUSquadDuty.Medic && agent.Target != null;
        if (!reposition || agent.Target == null && agent.RecoveryUntil <= now)
            return false;
        agent.NextDutyMove = now + TimeSpan.FromSeconds(4);
        if (!TraversablePassage(uid, Transform(uid).Coordinates, point) ||
            ExposureScore(uid, agent, point) > ExposureScore(uid, agent, Transform(uid).Coordinates) ||
            !TryReserveManeuver(uid, agent, now))
            return false;
        Decision(agent, "squad-move", agent.SquadPhase, 1);
        BeginMove(uid, agent, point, CMUExpeditionAgentState.Reposition, now);
        return true;
    }
}
