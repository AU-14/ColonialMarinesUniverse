using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private TimeSpan FireControlDuration(CMUExpeditionAgentComponent agent) =>
        agent.FireControlDuration > TimeSpan.Zero ? agent.FireControlDuration : agent.BurstDuration;

    private void UpdateFireControl(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var hasGun = _guns.TryGetGun(uid, out var gun);
        if (now < agent.NextFireControl && agent.FireControlObservedTarget == agent.Target &&
            agent.FireControlWeapon == (hasGun ? gun.Owner : (EntityUid?) null))
            return;
        agent.NextFireControl = now + ThinkInterval;
        agent.FireControlObservedTarget = agent.Target;
        agent.FireControlVolley = 0;
        agent.FireControlRecovery = null;
        agent.FireControlDuration = agent.BurstDuration;
        agent.SustainedFire = false;
        if (!hasGun)
        {
            agent.FireControlWeapon = null;
            agent.FireControlTargetVisible = false;
            return;
        }
        if (agent.FireControlWeapon != gun.Owner)
        {
            agent.FireControlWeapon = gun.Owner;
            agent.FireControlTargetVisible = false;
            agent.FireControlLastVisible = TimeSpan.Zero;
            agent.FireControlPeekUntil = TimeSpan.Zero;
        }
        var visible = agent.Target is { } target && CombatTargetAlive(target) &&
            AcceptOrderedContact(uid, agent, target) && Visible(uid, target, WeaponFireRange(uid, agent));
        if (!visible)
        {
            agent.FireControlTargetVisible = false;
            return;
        }
        var contact = agent.Target!.Value;
        if (agent.FireControlTarget != contact)
        {
            agent.FireControlTarget = contact;
            agent.FireControlTargetVisible = false;
            agent.FireControlLastVisible = TimeSpan.Zero;
            agent.FireControlPeekUntil = TimeSpan.Zero;
        }
        var reappeared = !agent.FireControlTargetVisible && agent.FireControlLastVisible > TimeSpan.Zero &&
            now - agent.FireControlLastVisible < TimeSpan.FromSeconds(3);
        agent.FireControlTargetVisible = true;
        agent.FireControlLastVisible = now;
        if (reappeared)
        {
            agent.FireControlPeekUntil = now + TimeSpan.FromSeconds(0.8);
            // An opponent returning to the angle interrupts a burst pause. This changes
            // readiness only; every attempt still needs fresh sight and a safe trajectory.
            agent.FireAt = now;
            agent.NextMovingBurst = now;
        }
        var enemyFiring = agent.FlashShooter == contact && now < agent.FlashUntil ||
            agent.RecentShooters.TryGetValue(contact, out var until) && now < until;
        var safe = SafeShot(uid, agent, gun, Transform(contact).Coordinates);
        var available = WeaponAmmo(gun);
        if (available <= 0)
            return;
        var fired = agent.MovingFire && CanFireWhileMoving(uid, agent) ? agent.MovingShotsFired :
            agent.State == CMUExpeditionAgentState.Engage ? agent.ShotsFired : 0;
        var context = new CMUInfantryFireContext(BaseVolleySize(agent), (float) agent.BurstDuration.TotalSeconds,
            (float) BaseRecoveryDelay(agent).TotalSeconds, available + fired,
            gun.Comp.SelectedMode == SelectiveFire.FullAuto &&
                (!TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role) || !role.Rocket),
            true, safe, agent.CoverAnchor == null, UrgentFire(agent, now) || now < agent.IncomingFireUntil,
            enemyFiring, now < agent.FireControlPeekUntil,
            Vector2.Distance(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(contact)));
        var plan = CMUInfantryFirePolicy.Decide(context);
        agent.FireControlVolley = plan.Volley;
        agent.FireControlDuration = TimeSpan.FromSeconds(plan.Duration);
        agent.FireControlRecovery = TimeSpan.FromSeconds(plan.Recovery);
        agent.SustainedFire = plan.Sustained;
        // Anchor extensions to the first actual shot. Losing and regaining pressure
        // during one volley must never repeatedly add time to the same deadline.
        if (agent.State == CMUExpeditionAgentState.Engage && agent.ShotsFired > 0 && agent.BurstEnd > now)
        {
            var deadline = agent.FireControlBurstStarted + agent.FireControlDuration;
            if (deadline > agent.BurstEnd)
                agent.BurstEnd = deadline;
        }
        if (agent.MovingFire && agent.MovingShotsFired > 0 && agent.MovingBurstEnd > now)
        {
            var deadline = agent.FireControlMovingBurstStarted + agent.FireControlDuration;
            if (deadline > agent.MovingBurstEnd)
                agent.MovingBurstEnd = deadline;
        }
    }

    private bool TrackedFireTarget(CMUExpeditionAgentComponent agent, TimeSpan now) =>
        agent.FireControlTargetVisible && agent.FireControlTarget == agent.Target &&
        agent.LastFireControlShotTarget == agent.Target &&
        agent.LastFiredWeapon == agent.FireControlWeapon && now - agent.LastShotAt < TimeSpan.FromSeconds(3);
}
