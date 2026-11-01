using Content.Shared.Hands;
using Content.Shared.SSDIndicator;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
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
        SubscribeLocalEvent<CMUExpeditionAgentComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusEffectAdded);
    }

    private void OnBeforeStatusEffectAdded(Entity<CMUExpeditionAgentComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        // AI bodies intentionally have no player. Only veto the disconnect timer;
        // anesthesia, forced sleep and other incapacitation retain their native ownership.
        if (args.Effect == SSDIndicatorSystem.StatusEffectSSDSleeping && !HasComp<ActorComponent>(ent))
            args.Cancelled = true;
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
        RequestThink(ent.Comp, _timing.CurTime);
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
                RequestThink(agent, _timing.CurTime);
            }
            return false;
        }
        if (agent.State != CMUExpeditionAgentState.Incapacitated)
        {
            CancelAgentActivity(uid, agent, "incapacitated");
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
        EntityUid? recovery = armed ? null : agent.Rifle;
        // A sidearm must not erase our memory of a primary lost to knockdown or a failed
        // hand operation. Secure nearby owned weapons during a lull, without dropping another.
        if (armed && LoadoutReadinessSafe(uid, agent, now))
        {
            foreach (var lost in agent.RememberedWeapons)
            {
                if (_containers.IsEntityOrParentInContainer(lost) || Transform(lost).Anchored ||
                    !Visible(uid, lost, agent.WeaponRecoveryRange) ||
                    TryComp<CMUExpeditionWeaponRoleComponent>(lost, out var role) && role.Rocket && WeaponAmmo(lost) == 0)
                    continue;
                recovery = lost;
                break;
            }
        }
        if (armed && recovery == null)
        {
            agent.WeaponRecoveryDecision = "armed";
            if (agent.State == CMUExpeditionAgentState.RecoverWeapon)
                FinishRecovery();
            return false;
        }
        if (recovery is not { } rifle || !Exists(rifle) ||
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
            var pickedUp = armed
                ? _inventory.TryEquip(uid, rifle, SuitStorageSlot, silent: true) || StoreSupply(uid, rifle)
                : _hands.TryPickupAnyHand(uid, rifle) && SelectHeldRifle(uid, agent);
            if (pickedUp)
            {
                agent.WeaponsRecovered++;
                agent.WeaponRecoveryDecision = "recovered";
                agent.NextWeaponChoice = now;
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
