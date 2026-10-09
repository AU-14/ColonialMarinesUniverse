using Content.Shared.Hands;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private void InitializeRecovery()
    {
        SubscribeLocalEvent<CMUExpeditionAgentComponent, DidEquipHandEvent>(OnWeaponEquipped);
        SubscribeLocalEvent<CMUExpeditionAgentComponent, DidUnequipHandEvent>(OnWeaponUnequipped);
    }

    private void OnWeaponEquipped(Entity<CMUExpeditionAgentComponent> ent, ref DidEquipHandEvent args)
    {
        if (!HasComp<ActorComponent>(ent) && HasComp<GunComponent>(args.Equipped))
        {
            ent.Comp.Rifle = args.Equipped;
            ent.Comp.RememberedWeapons.Add(args.Equipped);
        }
    }

    private void OnWeaponUnequipped(Entity<CMUExpeditionAgentComponent> ent, ref DidUnequipHandEvent args)
    {
        if (HasComp<ActorComponent>(ent) || !HasComp<GunComponent>(args.Unequipped))
            return;
        ent.Comp.Rifle = args.Unequipped;
        ent.Comp.NextWeaponRecovery = _timing.CurTime;
        ent.Comp.NextThink = _timing.CurTime;
    }

    private bool PauseForKnockdown(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var down = HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid);
        // Native knockdown owns its timer and stand-up do-after. Only request a normal stand
        // for bodies left prone without an active knockdown, e.g. after being revived.
        if (!down && _standing.IsDown(uid))
            down = !_standing.Stand(uid);
        if (!down)
        {
            if (agent.State == CMUExpeditionAgentState.Incapacitated)
            {
                agent.State = CMUExpeditionAgentState.Guard;
                agent.NextThink = _timing.CurTime;
            }
            return false;
        }
        if (agent.State != CMUExpeditionAgentState.Incapacitated)
        {
            CancelWork(uid, agent);
            CancelPlan(uid, agent, false);
            CancelTreatment(agent);
            ClearCover(agent);
            StopSpacing(uid, agent);
            agent.State = CMUExpeditionAgentState.Incapacitated;
            agent.LastFireCheck = "incapacitated";
        }
        _steering.Unregister(uid);
        return true;
    }

    private bool SelectHeldRifle(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (_guns.TryGetGun(uid, out var active))
        {
            agent.Rifle = active;
            return true;
        }
        foreach (var hand in _hands.EnumerateHands(uid))
        {
            if (!_hands.TryGetHeldItem(uid, hand, out var held) || !HasComp<GunComponent>(held))
                continue;
            _hands.TrySetActiveHand(uid, hand);
            agent.Rifle = held;
            return _guns.TryGetGun(uid, out _);
        }
        return false;
    }

    private bool RecoverWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        foreach (var carried in CarriedWeapons(uid))
            agent.RememberedWeapons.Add(carried);
        agent.RememberedWeapons.RemoveWhere(weapon => !Exists(weapon));
        var armed = SelectHeldRifle(uid, agent);
        // A sidearm must not erase our memory of a primary lost to knockdown or a failed
        // hand operation. Secure nearby owned weapons during a lull, without dropping another.
        if (armed && agent.Action == null && agent.PendingWeapon == null && agent.Treatment == null &&
            agent.WorkItem == null && !agent.PreparingWork && agent.FlareItem == null && agent.Target == null &&
            now - agent.LastHit > TimeSpan.FromSeconds(2) && now >= agent.NextWeaponRecovery)
        {
            foreach (var lost in agent.RememberedWeapons)
            {
                if (_containers.IsEntityOrParentInContainer(lost) || Transform(lost).Anchored ||
                    !_interaction.InRangeUnobstructed(uid, lost) || !Visible(uid, lost, 2) ||
                    TryComp<CMUExpeditionWeaponRoleComponent>(lost, out var role) && role.Rocket && WeaponAmmo(lost) == 0)
                    continue;
                agent.NextWeaponRecovery = now + TimeSpan.FromSeconds(2);
                if (_inventory.TryEquip(uid, lost, "suitStorage", silent: true) || StoreSupply(uid, lost))
                {
                    agent.WeaponsRecovered++;
                    agent.WeaponRecoveryDecision = "secured-lost-primary";
                    agent.NextWeaponChoice = now;
                }
                break;
            }
        }
        if (armed)
        {
            agent.WeaponRecoveryDecision = "armed";
            if (agent.State == CMUExpeditionAgentState.RecoverWeapon)
                FinishRecovery();
            return false;
        }
        if (agent.Rifle is not { } rifle || !Exists(rifle) ||
            WeaponAmmo(rifle) == 0 && SpareAmmunition(uid, rifle) == null ||
            !TryComp(rifle, out TransformComponent? weaponTransform) ||
            _containers.IsEntityOrParentInContainer(rifle) || weaponTransform.Anchored ||
            !Visible(uid, rifle, agent.WeaponRecoveryRange))
        {
            agent.WeaponRecoveryDecision = "weapon-unavailable";
            if (agent.State == CMUExpeditionAgentState.RecoverWeapon)
                FinishRecovery();
            return false;
        }
        // Utility actions need their working hand, but cannot finish after their gun is lost.
        if (agent.Action != null)
            CancelPlan(uid, agent, false);
        CancelWork(uid, agent);
        CancelTreatment(agent);
        if (_interaction.InRangeUnobstructed(uid, rifle))
        {
            if (now < agent.NextWeaponRecovery)
                return false;
            agent.NextWeaponRecovery = now + TimeSpan.FromSeconds(0.5);
            if (_hands.TryPickupAnyHand(uid, rifle) && SelectHeldRifle(uid, agent))
            {
                agent.WeaponsRecovered++;
                agent.WeaponRecoveryDecision = "recovered";
                FinishRecovery();
            }
            else
                agent.WeaponRecoveryDecision = "pickup-blocked";
            return false;
        }
        // Recover a gun at our feet even under pressure; never chase a distant one into a rush.
        if (agent.RushTarget != null || now < agent.SpacingUntil || !GroundSafe(weaponTransform.Coordinates))
        {
            agent.WeaponRecoveryDecision = "unsafe-approach";
            if (agent.State == CMUExpeditionAgentState.RecoverWeapon)
                FinishRecovery();
            return false;
        }
        if (agent.State == CMUExpeditionAgentState.RecoverWeapon)
        {
            if (agent.CoverDestination is { } destination &&
                _transform.InRange(destination, weaponTransform.Coordinates, 1) && ContinueMove(uid, agent, Transform(uid), now))
                return true;
            FinishRecovery();
            agent.NextWeaponRecovery = now + TimeSpan.FromSeconds(2);
            return false;
        }
        if (now < agent.NextWeaponRecovery)
            return false;
        agent.NextWeaponRecovery = now + TimeSpan.FromSeconds(2);
        CancelTreatment(agent);
        ClearCover(agent);
        if (!BuildTacticalRoute(uid, agent, weaponTransform.Coordinates))
        {
            agent.WeaponRecoveryDecision = "weapon-route-blocked";
            return false;
        }
        agent.WeaponRecoveryDecision = "retrieving";
        agent.NextWeaponRecovery = now;
        BeginMove(uid, agent, weaponTransform.Coordinates, CMUExpeditionAgentState.RecoverWeapon, now);
        return true;

        void FinishRecovery()
        {
            _steering.Unregister(uid);
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            agent.HoldUntil = now;
        }
    }
}
