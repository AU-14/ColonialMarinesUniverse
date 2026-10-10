using System.Linq;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedPumpActionSystem _weaponPump = default!;

    private bool HasUsableCarriedAmmo(EntityUid uid) =>
        CarriedWeapons(uid).Any(weapon => WeaponAmmo(weapon) > 0 || SpareAmmunition(uid, weapon) != null);

    // Ammunition can occupy a pocket or the spare hand directly, as well as a bag.
    private IEnumerable<EntityUid> CarriedAmmunition(EntityUid uid)
    {
        var items = new HashSet<EntityUid>(SupplyItems(uid));
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var held))
                items.Add(held.Value);
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.MoveNext(out var slot))
            if (slot.ContainedEntity is { } item)
                items.Add(item);
        return items;
    }

    private EntityUid? ReloadAmmunition(EntityUid uid, EntityUid? weapon = null)
    {
        if (weapon == null && _guns.TryGetGun(uid, out var active))
            weapon = active;
        if (weapon is not { } gun || SpareAmmunition(uid, gun) is not { } spare)
            return null;
        // Do not replace a loaded magazine just to tidy the inventory. A tube can be
        // topped off, but a full tube must not start another failed insertion do-after.
        if (_itemSlots.TryGetSlot(gun, "gun_magazine", out _))
            return WeaponAmmo(gun) == 0 ? spare : null;
        return TryComp<BallisticAmmoProviderComponent>(gun, out var tube) &&
            _guns.CanInsertBallistic((gun, tube), spare) ? spare : null;
    }

    private bool LoadoutReadinessSafe(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now) =>
        agent.PlanningEnabled && agent.Action == null && agent.Treatment == null && agent.WorkItem == null &&
        !agent.PreparingWork && agent.FlareItem == null && agent.UtilityCleanupItem == null && agent.FireRescueTarget == null &&
        agent.LastStandTarget == null && agent.PendingMeleeWeapon == null && agent.ScavengeTarget == null &&
        agent.RushTarget == null && agent.SpacingDestination == null && !agent.CornerHolding && agent.Target == null &&
        now >= agent.ForgetAt && now >= agent.SuppressedUntil && now - agent.LastContact >= TimeSpan.FromSeconds(4) &&
        now - agent.LastHit >= TimeSpan.FromSeconds(2) && agent.LastDamage < agent.HealDamage &&
        (!agent.FailedActions.TryGetValue(CMUTacticalAction.Reload, out var failedUntil) || now >= failedUntil) &&
        !HasCoverCommitment(uid, agent, now) && !GrenadeDanger(Transform(uid).Coordinates) && TreatmentSafe(uid, agent);

    private EntityUid? LoadoutReadinessWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!LoadoutReadinessSafe(uid, agent, now))
            return null;
        if (_guns.TryGetGun(uid, out var active) && ReloadAmmunition(uid, active) != null)
            return active;
        EntityUid? chosen = null;
        var best = float.MinValue;
        foreach (var weapon in CarriedWeapons(uid))
        {
            if (ReloadAmmunition(uid, weapon) == null ||
                TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) && role.Rocket)
                continue;
            var priority = role?.Priority ?? 20;
            if (priority <= best)
                continue;
            chosen = weapon;
            best = priority;
        }
        return chosen;
    }

    private bool RunWeaponReadiness(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.PendingWeapon != null || !LoadoutReadinessSafe(uid, agent, now) ||
            !_guns.TryGetGun(uid, out var gun) || ReloadAmmunition(uid, gun) == null)
            return false;
        // The existing plan owns movement cancellation, real hand use and the native
        // reload. Its Armed goal means ready for this preparation, even for a partial tube.
        return RunPlan(uid, agent, false, agent.LastDamage, false, now);
    }

    private bool PrepareNativeWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid weapon, TimeSpan now)
    {
        if (now < agent.NativeWeaponReadyAt)
            return false;
        var pumpNeeded = TryComp<PumpActionComponent>(weapon, out var pump) && !pump.Pumped &&
            (!TryComp<GunComponent>(weapon, out var gun) || !gun.BurstActivated);
        var chamberNeeded = TryComp<ChamberMagazineAmmoProviderComponent>(weapon, out var chamber) &&
            (chamber.BoltClosed == false || _guns.GetChamberEntity(weapon) is not { } round ||
                TryComp<CartridgeAmmoComponent>(round, out var cartridge) && cartridge.Spent);
        if (!pumpNeeded && !chamberNeeded)
            return true;
        var movingAction = agent.State == CMUExpeditionAgentState.PlanMove &&
            (agent.Action is CMUTacticalAction.TakeCover or CMUTacticalAction.Flank ||
                agent.Action == CMUTacticalAction.ThrowGrenade && agent.GrenadeThrowStance != null);
        if (agent.Action != null && !movingAction || agent.Treatment != null || agent.PendingWeapon != null ||
            agent.FlareItem != null || agent.UtilityCleanupItem != null ||
            agent.FireRescueTarget != null || agent.LastStandTarget != null || agent.PendingMeleeWeapon != null ||
            agent.WorkItem != null || agent.PreparingWork || agent.ScavengeTarget != null ||
            !_hands.IsHolding(uid, weapon, out _) || WeaponAmmo(weapon) <= 0)
            return false;
        if (pumpNeeded)
            _weaponPump.Pump((weapon, pump!), uid);
        else if (chamber!.BoltClosed == false || chamber.CanRack)
            _guns.UseChambered(weapon, chamber, uid);
        else
            return false;
        agent.NativeWeaponReadyAt = now + TimeSpan.FromSeconds(0.25);
        agent.WeaponDecision = "cycling-weapon";
        return false;
    }
}
