using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    // A carried utility does not occupy the gun hand. Keep firing an already active
    // one-handed gun; never draw another weapon or bypass a required two-handed grip.
    private bool CanFireDuringUtility(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        agent.FireRescueTarget == null && agent.PendingWeapon == null && agent.ScavengeTarget == null &&
        agent.Treatment == null && agent.TreatmentMedicine == null && agent.WorkItem == null && !agent.PreparingWork &&
        (agent.FlareItem != null && agent.Action == null ||
            agent.Action == CMUTacticalAction.ThrowGrenade && agent.ActionItem != null &&
            agent.State == CMUExpeditionAgentState.Throwing) &&
        _guns.TryGetGun(uid, out var gun) && _hands.IsHolding(uid, gun.Owner, out _) &&
        !HasComp<GunRequiresWieldComponent>(gun) && WeaponAmmo(gun) > 0;

    private bool CanFireWhileMoving(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var utilityFire = CanFireDuringUtility(uid, agent);
        return agent.FireRescueTarget == null && (agent.FlareItem == null || utilityFire) &&
            agent.PendingWeapon == null && agent.ScavengeTarget == null && agent.Treatment == null && agent.TreatmentMedicine == null &&
            agent.WorkItem == null && !agent.PreparingWork &&
            (agent.Action == null || agent.Action is CMUTacticalAction.Flank or CMUTacticalAction.TakeCover ||
                agent.Action == CMUTacticalAction.ThrowGrenade &&
                (utilityFire || agent.GrenadeThrowStance != null && agent.State == CMUExpeditionAgentState.PlanMove)) &&
            (agent.State is CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Investigate or
                CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Withdraw or
                CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.PlanMove or CMUExpeditionAgentState.Watch ||
                utilityFire && agent.State == CMUExpeditionAgentState.Throwing);
    }

    private void UpdateMovingFire(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        agent.MovingFire = true;
        if (UrgentFire(agent, now) && agent.NextMovingBurst > now + TimeSpan.FromSeconds(0.1))
            agent.NextMovingBurst = now + TimeSpan.FromSeconds(0.1);
        if (now < agent.NextMovingBurst)
            return;
        if (now >= agent.MovingBurstEnd || agent.MovingShotsFired >= VolleySize(agent))
        {
            agent.MovingShotsFired = 0;
            agent.MovingBurstEnd = now + FireControlDuration(agent);
        }
        if (!_guns.TryGetGun(uid, out var gun) || !PrepareNativeWeapon(uid, agent, gun, now) || !_guns.CanShoot(gun))
        {
            agent.LastFireCheck = "moving-weapon-not-ready";
            return;
        }
        if (!TryAimPoint(uid, agent, gun, out var point))
        {
            agent.LastFireCheck = "moving-no-visible-aim-point";
            return;
        }
        if (!SafeShot(uid, agent, gun, point))
        {
            agent.LastFireCheck = "moving-lane-blocked";
            agent.BlockedShotSince ??= now;
            if (agent.TrafficNudgeDestination != null && now < agent.TrafficNudgeUntil)
                return;
            if (TryResolveDoorFiringLane(uid, agent, point, now))
                return;
            // A stalled travel leg must not strand a ready rifle behind a pole or ally.
            // Escape/utility actions retain priority; ordinary contact can step out once.
            if (agent.Action == null && agent.SpacingDestination == null &&
                (agent.TrafficNudgeDestination == null || now >= agent.TrafficNudgeUntil) &&
                now - agent.BlockedShotSince >= TimeSpan.FromSeconds(0.3) &&
                (now - agent.LastHit < TimeSpan.FromSeconds(1) ||
                    !TryComp<NPCSteeringComponent>(uid, out var steering) || steering.Status != SteeringStatus.Moving))
            {
                if (TryAdjustPeek(uid, agent, point, now))
                    agent.ContactDestination = null;
            }
            return;
        }
        agent.BlockedShotSince = null;
        // A usable lane needs no stop or arrival. Native gun systems still enforce
        // wielding, recoil and fire rate; only a blocked lane requests a sidestep above.
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combat.SetInCombatMode(uid, true, combat);
        var direction = _transform.ToMapCoordinates(point).Position - _transform.GetWorldPosition(uid);
        _transform.SetWorldRotation(uid, direction.ToWorldAngle());
        agent.LastFireCheck = _guns.AttemptShoot(uid, gun, point, agent.FiringAtFlash ? null : agent.Target)
            ? "moving-trigger-accepted" : "moving-native-trigger-rejected";
    }

    private bool MovingShotAllowed(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        agent.MovingFire && CanFireWhileMoving(uid, agent) && _timing.CurTime >= agent.NextMovingBurst &&
        _timing.CurTime < agent.MovingBurstEnd && agent.MovingShotsFired < VolleySize(agent);

    private static bool UrgentFire(CMUExpeditionAgentComponent agent, TimeSpan now) =>
        agent.RushTarget != null || now < agent.ImmediateFireUntil || now < agent.SuppressedUntil ||
        now - agent.LastHit < TimeSpan.FromSeconds(0.75);
}
