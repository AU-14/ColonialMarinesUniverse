using Content.Shared.DoAfter;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.Stacks;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private void InitializeMedicine()
    {
        SubscribeLocalEvent<CMUExpeditionAgentComponent, HealingDoAfterEvent>(OnTreatmentFinished,
            after: new[] { typeof(HealingSystem) });
    }

    private bool TreatmentSafe(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        _npcs.Enabled && !HasComp<ActorComponent>(uid) && _mobs.IsAlive(uid) &&
        agent.RushTarget == null && !GrenadeDanger(Transform(uid).Coordinates) &&
        GroundSafe(Transform(uid).Coordinates) && _doorActions.CanInteract(uid, uid) &&
        _timing.CurTime - agent.LastHit >= TimeSpan.FromSeconds(0.75) &&
        ShelteredFromKnownThreats(uid, agent, Transform(uid).Coordinates);

    private bool TryTreat(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        if (agent.Treatment != null || agent.TreatmentMedicine != null)
            return ContinueTreatment(uid, agent, damage, false, now);
        if (agent.FlareItem != null || agent.PendingWeapon != null || agent.UtilityCleanupItem != null ||
            agent.Action != null && agent.Action != CMUTacticalAction.Treat ||
            !ShouldTreat(agent, damage, now) || !TreatmentSafe(uid, agent) ||
            PersonalDressing(uid) is not { } medicine ||
            !TryComp<HealingComponent>(medicine, out var healing) ||
            TryComp<StackComponent>(medicine, out var stack) && stack.Count <= 0)
            return false;

        // Native unwielding queues virtual-grip deletion. Claim the hands before that
        // deletion completes so weapon readiness cannot take them back between thinks.
        StopSpacing(uid, agent);
        ClearTraffic(agent);
        CancelAimedWeapon(agent);
        agent.TreatmentMedicine = medicine;
        agent.TreatmentPreparingUntil = now + TimeSpan.FromSeconds(0.6);
        agent.RifleLoweredUntil = agent.TreatmentPreparingUntil;
        agent.State = CMUExpeditionAgentState.Healing;
        _steering.Unregister(uid);
        if (_guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
        return ContinueTreatment(uid, agent, damage, false, now);
    }

    private bool ContinueTreatment(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, bool hit, TimeSpan now)
    {
        if (agent.Treatment == null && agent.TreatmentMedicine == null)
            return false;
        if (hit || !TreatmentSafe(uid, agent))
            return Abort();
        if (agent.Treatment is { } current)
        {
            // Completion status can precede its native event by one system update.
            // The callback owns the applied dose; do not start another one in that gap.
            if (_doAfter.GetStatus(current) is DoAfterStatus.Invalid or DoAfterStatus.Cancelled)
                return Abort();
            agent.State = CMUExpeditionAgentState.Healing;
            _steering.Unregister(uid);
            return true;
        }
        if (agent.TreatmentMedicine is not { } medicine || PersonalDressing(uid) != medicine ||
            !ShouldTreat(agent, damage, now) || !TryComp<HealingComponent>(medicine, out var healing) ||
            TryComp<StackComponent>(medicine, out var stack) && stack.Count <= 0)
            return Abort();
        if (_hands.GetEmptyHandCount(uid) == 0)
        {
            if (now >= agent.TreatmentPreparingUntil)
                return Abort();
            return true;
        }
        _steering.Unregister(uid);
        var args = new DoAfterArgs(EntityManager, uid, healing.Delay, new HealingDoAfterEvent(), uid,
            target: uid, used: medicine)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = 0.1f,
            ExtraCheck = () => TreatmentSafe(uid, agent) &&
                agent.TreatmentMedicine == medicine && PersonalDressing(uid) == medicine,
        };
        if (!_doAfter.TryStartDoAfter(args, out var id))
            return Abort();
        agent.Treatment = id;
        agent.TreatmentPreparingUntil = TimeSpan.Zero;
        agent.State = CMUExpeditionAgentState.Healing;
        return true;

        bool Abort()
        {
            CancelTreatment(agent);
            if (agent.Action == CMUTacticalAction.Treat)
                CancelPlan(uid, agent, true);
            agent.NextRetreat = now;
            return false;
        }
    }

    private bool HasMedicine(EntityUid uid) => PersonalDressing(uid) != null;

    private static bool ShouldTreat(CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        if (damage < agent.HealDamage || now < agent.NextHeal)
            return false;
        if (damage >= agent.EmergencyHealDamage)
            return true;
        // Minor wounds do not interrupt every firing cycle. Prefer a lull or a buddy covering treatment.
        if (now - agent.LastHit < TimeSpan.FromSeconds(4 + agent.Aggression * 2))
            return false;
        if (agent.Target == null || now >= agent.ForgetAt || now - agent.LastContact >= TimeSpan.FromSeconds(6))
            return true;
        if (agent.WoundedSince is not { } wounded || now - wounded < agent.HealOpportunityDelay)
            return false;
        return agent.HasCoveringAlly || now - wounded >= agent.HealOpportunityDelay + TimeSpan.FromSeconds(6);
    }

    private void OnTreatmentFinished(Entity<CMUExpeditionAgentComponent> ent, ref HealingDoAfterEvent args)
    {
        if (ent.Comp.Treatment != args.DoAfter.Id)
            return;
        // Native healing applies damage/bleeding changes and consumes one actual dressing.
        // Reassess threats between doses instead of letting its automatic repeat keep us immobilized.
        args.Repeat = false;
        ent.Comp.Treatment = null;
        ent.Comp.TreatmentMedicine = null;
        ent.Comp.TreatmentPreparingUntil = TimeSpan.Zero;
        ent.Comp.NextHeal = _timing.CurTime + TimeSpan.FromSeconds(0.4);
        if (!args.Cancelled)
            ent.Comp.LastDamage = _damage.GetTotalDamage(ent.Owner).Float();
        if (ent.Comp.State == CMUExpeditionAgentState.Healing)
        {
            ent.Comp.State = CMUExpeditionAgentState.Retreat;
            ent.Comp.HoldUntil = _timing.CurTime + TimeSpan.FromSeconds(0.6);
        }
        if (ent.Comp.Action == CMUTacticalAction.Treat)
        {
            if (args.Cancelled || !args.Handled)
                CancelPlan(ent, ent.Comp, true);
            else
                ent.Comp.ActionComplete = true;
        }
    }

    private void CancelTreatment(CMUExpeditionAgentComponent agent)
    {
        var owned = agent.Treatment != null || agent.TreatmentMedicine != null;
        var treatment = agent.Treatment;
        agent.Treatment = null;
        agent.TreatmentMedicine = null;
        agent.TreatmentPreparingUntil = TimeSpan.Zero;
        if (owned)
        {
            agent.NextHeal = _timing.CurTime + TimeSpan.FromSeconds(1);
            if (agent.State == CMUExpeditionAgentState.Healing)
                agent.State = CMUExpeditionAgentState.Guard;
        }
        if (treatment is { } id && _doAfter.GetStatus(id) == DoAfterStatus.Running)
            _doAfter.Cancel(id);
    }
}
