using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.CMU14.Expeditions;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool CanFireWhileMoving(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        agent.PendingWeapon == null && agent.Treatment == null && agent.WorkItem == null && !agent.PreparingWork &&
        (agent.Action == null || agent.Action is CMUTacticalAction.Flank or CMUTacticalAction.TakeCover) &&
        agent.State is CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Investigate or
            CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Withdraw or
            CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.PlanMove &&
        (TryComp<NPCSteeringComponent>(uid, out var steering) && steering.Status != SteeringStatus.NoPath ||
            agent.TrafficWaitingSince != null && agent.TrafficActiveUntil > _timing.CurTime);

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
            agent.MovingBurstEnd = now + agent.BurstDuration;
        }
        if (!_guns.TryGetGun(uid, out var gun) || !_guns.CanShoot(gun))
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
            return;
        }
        // This executor does not change navigation state, unregister steering, or wait
        // for arrival. Native gun systems still enforce wielding, recoil and fire rate.
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combat.SetInCombatMode(uid, true, combat);
        var direction = _transform.ToMapCoordinates(point).Position - _transform.GetWorldPosition(uid);
        _transform.SetWorldRotation(uid, direction.ToWorldAngle());
        agent.LastFireCheck = _guns.AttemptShoot(uid, gun, point, agent.Target)
            ? "moving-trigger-accepted" : "moving-native-trigger-rejected";
    }

    private bool MovingShotAllowed(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        agent.MovingFire && CanFireWhileMoving(uid, agent) && _timing.CurTime >= agent.NextMovingBurst &&
        _timing.CurTime < agent.MovingBurstEnd && agent.MovingShotsFired < VolleySize(agent);

    private static bool UrgentFire(CMUExpeditionAgentComponent agent, TimeSpan now) =>
        agent.RushTarget != null || now < agent.ImmediateFireUntil || now < agent.SuppressedUntil ||
        now - agent.LastHit < TimeSpan.FromSeconds(0.75);
}
