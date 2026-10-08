using Content.Shared.DoAfter;
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
        ShelteredFromKnownThreats(uid, agent, Transform(uid).Coordinates);

    private bool TryTreat(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        if (!ShouldTreat(agent, damage, now) || !TreatmentSafe(uid, agent) ||
            !_inventory.TryGetSlotEntity(uid, "pocket1", out var medicine) ||
            !TryComp<HealingComponent>(medicine, out var healing) ||
            TryComp<StackComponent>(medicine, out var stack) && stack.Count <= 0)
            return false;

        // Unshoulder the rifle to free a hand; the dressing stays in its accessible medical pocket.
        agent.RifleLoweredUntil = now + TimeSpan.FromSeconds(0.3);
        if (_guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
        if (_hands.GetEmptyHandCount(uid) == 0)
            return false;

        _steering.Unregister(uid);
        var args = new DoAfterArgs(EntityManager, uid, healing.Delay, new HealingDoAfterEvent(), uid,
            target: uid, used: medicine)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = 0.1f,
            ExtraCheck = () => TreatmentSafe(uid, agent) &&
                _inventory.TryGetSlotEntity(uid, "pocket1", out var current) && current == medicine,
        };
        if (!_doAfter.TryStartDoAfter(args, out var id))
            return false;
        agent.Treatment = id;
        agent.State = CMUExpeditionAgentState.Healing;
        return true;
    }

    private bool HasMedicine(EntityUid uid) =>
        _inventory.TryGetSlotEntity(uid, "pocket1", out var medicine) && HasComp<HealingComponent>(medicine) &&
        (!TryComp<StackComponent>(medicine, out var stack) || stack.Count > 0);

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
        ent.Comp.NextHeal = _timing.CurTime + TimeSpan.FromSeconds(0.4);
        if (!args.Cancelled)
            ent.Comp.LastDamage = _damage.GetTotalDamage(ent.Owner).Float();
        if (ent.Comp.State == CMUExpeditionAgentState.Healing)
        {
            ent.Comp.State = CMUExpeditionAgentState.Retreat;
            ent.Comp.HoldUntil = _timing.CurTime + TimeSpan.FromSeconds(0.6);
        }
    }

    private void CancelTreatment(CMUExpeditionAgentComponent agent)
    {
        if (agent.Treatment is not { } id)
            return;
        agent.Treatment = null;
        if (_doAfter.GetStatus(id) == DoAfterStatus.Running)
            _doAfter.Cancel(id);
    }
}
