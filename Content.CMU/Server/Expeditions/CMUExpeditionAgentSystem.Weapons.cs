using System.Numerics;
using Content.Shared.Directions;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private float WeaponFireRange(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        _guns.TryGetGun(uid, out var gun) && TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role)
            ? Math.Min(agent.FireRange, role.MaximumRange) : agent.FireRange;

    private int WeaponAmmo(EntityUid weapon)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(weapon, ref ammo);
        return ammo.Count;
    }

    private List<EntityUid> CarriedWeapons(EntityUid uid)
    {
        var weapons = new List<EntityUid>();
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var item) && HasComp<GunComponent>(item))
                weapons.Add(item.Value);
        if (_inventory.TryGetSlotEntity(uid, "suitStorage", out var slung) && HasComp<GunComponent>(slung))
            weapons.Add(slung.Value);
        if (Supplies(uid, out var supplies))
            foreach (var item in supplies.Container.ContainedEntities)
                if (HasComp<GunComponent>(item))
                    weapons.Add(item);
        return weapons;
    }

    private bool ChooseWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.Action != null || agent.Treatment != null || agent.WorkItem != null || agent.PreparingWork)
            return false;
        _guns.TryGetGun(uid, out var current);
        if (agent.PendingWeapon is { } pending)
        {
            if (now < agent.WeaponSwitchAt)
                return true;
            agent.PendingWeapon = null;
            agent.NextWeaponChoice = now + TimeSpan.FromSeconds(3);
            // Revalidate possession and usefulness after freeing the wielding hand.
            if (!CarriedWeapons(uid).Contains(pending) || WeaponScore(uid, agent, pending) < 0)
                return false;
            var wasSlung = _inventory.TryGetSlotEntity(uid, "suitStorage", out var slung) && slung == pending;
            if (!_hands.IsHolding(uid, pending, out _) && !_hands.TryPickupAnyHand(uid, pending))
                return false;
            if (current.Owner.IsValid() && current.Owner != pending && !StowWeapon(uid, current) &&
                HasComp<GunRequiresWieldComponent>(pending))
            {
                // Roll back to the source slot; never discard the primary to force a swap.
                if (wasSlung)
                    _inventory.TryEquip(uid, pending, "suitStorage", silent: true);
                else if (Supplies(uid, out var bag))
                    _hands.TryDropIntoContainer(uid, pending, bag.Container);
                ActivateWeapon(uid, current);
                agent.WeaponDecision = "stow-blocked";
                return false;
            }
            ActivateWeapon(uid, pending);
            agent.Rifle = pending;
            agent.WeaponBurstLimit = TryComp<CMUExpeditionWeaponRoleComponent>(pending, out var role) ? role.BurstLimit : int.MaxValue;
            agent.WeaponSwitches++;
            agent.WeaponDecision = role?.Rocket == true ? "rocket" : "firearm";
            agent.NextWeaponChoice = now + TimeSpan.FromSeconds(2);
            agent.NextPlan = now + TimeSpan.FromSeconds(0.5);
            ClearCover(agent);
            Aim(agent, now);
            return true;
        }
        if (now < agent.NextWeaponChoice)
            return false;
        agent.NextWeaponChoice = now + TimeSpan.FromSeconds(0.5);
        var best = current.Owner.IsValid() ? WeaponScore(uid, agent, current) + 4 : -1;
        EntityUid? chosen = null;
        foreach (var weapon in CarriedWeapons(uid))
        {
            var score = WeaponScore(uid, agent, weapon);
            if (score < 0 || score <= best || weapon == current.Owner)
                continue;
            chosen = weapon;
            best = score;
        }
        if (chosen is not { } next)
            return false;
        if (current.Owner.IsValid())
            _wield.TryUnwield(current.Owner, uid);
        agent.PendingWeapon = next;
        agent.WeaponSwitchAt = now + TimeSpan.FromSeconds(0.25);
        agent.RifleLoweredUntil = agent.WeaponSwitchAt;
        agent.WeaponDecision = "switching";
        return true;
    }

    private bool StowWeapon(EntityUid uid, EntityUid weapon) =>
        TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) && role.Rocket && WeaponAmmo(weapon) == 0 &&
            _hands.TryDrop(uid, weapon) ||
        _inventory.TryEquip(uid, weapon, "suitStorage", silent: true) ||
        Supplies(uid, out var bag) && _hands.TryDropIntoContainer(uid, weapon, bag.Container);

    private void ActivateWeapon(EntityUid uid, EntityUid weapon)
    {
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var held) && held == weapon)
                _hands.TrySetActiveHand(uid, hand);
    }

    private float WeaponScore(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid weapon)
    {
        if (!Exists(weapon) || !TryComp<GunComponent>(weapon, out var gun))
            return -100;
        if (WeaponAmmo(weapon) == 0 && (!TreatmentSafe(uid, agent) || SpareMagazine(uid, weapon) == null))
            return -100;
        TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role);
        var score = role?.Priority ?? 20;
        if (agent.Target is not { } target || !Visible(uid, target, agent.FireRange))
            return role?.Rocket == true ? -100 : score;
        var point = Transform(target).Coordinates;
        var distance = Vector2.Distance(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(target));
        if (role != null)
        {
            if (distance < role.MinimumRange || distance > role.MaximumRange)
                return -100;
            if (distance < role.CloseRange)
                score += role.ClosePriority;
            if (role.Rocket && (!RocketOpportunity(uid, agent, target, point) ||
                !SafeShot(uid, agent, gun, point)))
                return -100;
        }
        return score;
    }

    private bool RocketOpportunity(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target, EntityCoordinates point)
    {
        if (_timing.CurTime < agent.NextRocket || agent.RushTarget != null || agent.SpacingDestination != null)
            return false;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !SameSquad(uid, agent, other, buddy))
                continue;
            if (_timing.CurTime < buddy.NextRocket || buddy.PendingWeapon is { } pending &&
                TryComp<CMUExpeditionWeaponRoleComponent>(pending, out var role) && role.Rocket ||
                _guns.TryGetGun(other, out var gun) && TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var active) && active.Rocket &&
                buddy.Target != null && _mobs.IsAlive(other))
                return false;
        }
        // Save the one-shot tube for clustered contacts or an entrenched shooter which
        // has punished repeated peeks. A close rush needs the ready firearm instead.
        if (agent.RepeatedPeekHits >= 2)
            return true;
        var contacts = 0;
        foreach (var threat in agent.VisibleThreats)
            if (_transform.InRange(point, threat, 3) && ++contacts >= 2)
                return true;
        return false;
    }

    private bool SafeWeaponEffect(EntityUid uid, CMUExpeditionAgentComponent agent, GunComponent gun,
        EntityCoordinates start, EntityCoordinates destination)
    {
        if (!TryComp<CMUExpeditionWeaponRoleComponent>(gun.Owner, out var role))
            return true;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(destination);
        var distance = Vector2.Distance(from.Position, to.Position);
        if (distance < role.MinimumRange || distance > role.MaximumRange)
            return false;
        if (!role.Rocket)
            return true;
        if (_timing.CurTime < agent.NextRocket || !SafeBlast(uid, destination, role.BlastRadius, 0.4f) ||
            !ClearLane(uid, start, destination, 0.6f))
            return false;
        // Native CMU backblast affects the two cardinal tiles behind the shooter.
        var rear = (to.Position - from.Position).ToWorldAngle().GetCardinalDir().GetOpposite().ToVec();
        var behind = _transform.ToCoordinates(start.EntityId, from.Offset(rear * 2));
        if (!ClearLane(uid, start, behind, 0.8f))
            return false;
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(from.MapId, from.Position, distance + 2, nearby);
        foreach (var entity in nearby)
        {
            if (entity == uid || !HasComp<MobStateComponent>(entity) || _mobs.IsDead(entity))
                continue;
            var position = _transform.GetWorldPosition(entity);
            var velocity = TryComp<PhysicsComponent>(entity, out var body) ? body.LinearVelocity : Vector2.Zero;
            var forward = Vector2.Normalize(to.Position - from.Position);
            var along = Vector2.Dot(position - from.Position, forward);
            // A nearer body may detonate the rocket before its intended destination.
            if (along > 0 && along < distance &&
                Vector2.Distance(position, from.Position + forward * along) < 0.8f &&
                !SafeBlast(uid, Transform(entity).Coordinates, role.BlastRadius, 0.4f))
                return false;
            if (!IsFriendly(uid, entity))
                continue;
            for (var sample = 0; sample < 2; sample++)
                if (Vector2.Distance(position + velocity * (sample * 0.4f), from.Position + rear) < 1.5f ||
                    Vector2.Distance(position + velocity * (sample * 0.4f), from.Position + rear * 2) < 1.5f)
                    return false;
        }
        return true;
    }
}
