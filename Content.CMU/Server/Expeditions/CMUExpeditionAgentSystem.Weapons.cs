using System.Numerics;
using Content.Shared._RMC14.Weapons.Ranged.Chamber;
using Content.Shared.Directions;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    // Inventory slot IDs are case-sensitive; this matches the human inventory template.
    private const string SuitStorageSlot = "suitstorage";

    private float WeaponFireRange(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        _guns.TryGetGun(uid, out var gun) && TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role)
            ? role.Rocket && VehicleAimBody(uid) != null ? agent.FireRange : Math.Min(agent.FireRange, role.MaximumRange)
            : agent.FireRange;

    private int WeaponAmmo(EntityUid weapon)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(weapon, ref ammo);
        // RMC's reserve chamber supplies a real round after magazine removal, but its
        // system does not contribute that round to the magazine's count event.
        if (TryComp<RMCGunChamberComponent>(weapon, out var rmcChamber) && rmcChamber.Enabled &&
            _containers.TryGetContainer(weapon, rmcChamber.ContainerId, out var reserve))
            foreach (var round in reserve.ContainedEntities)
                if (!TryComp<CartridgeAmmoComponent>(round, out var cartridge) || !cartridge.Spent)
                    ammo.Count++;
        if (HasComp<ChamberMagazineAmmoProviderComponent>(weapon) && _guns.GetChamberEntity(weapon) is { } chambered &&
            TryComp<CartridgeAmmoComponent>(chambered, out var spent) && spent.Spent)
            ammo.Count = Math.Max(0, ammo.Count - 1);
        return ammo.Count;
    }

    private List<EntityUid> CarriedWeapons(EntityUid uid)
    {
        var weapons = new List<EntityUid>();
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var item) && HasComp<GunComponent>(item))
                weapons.Add(item.Value);
        // Include directly equipped guns as well as the sling: custom kits may holster a
        // backup on the belt instead of putting it inside one of the supply containers.
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.MoveNext(out var slot))
            if (slot.ContainedEntity is { } equipped && HasComp<GunComponent>(equipped))
                weapons.Add(equipped);
        foreach (var item in SupplyItems(uid))
            if (HasComp<GunComponent>(item))
                weapons.Add(item);
        return weapons;
    }

    private bool ChooseWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.Action != null || agent.Treatment != null || agent.WorkItem != null || agent.PreparingWork ||
            agent.FlareItem != null || agent.UtilityCleanupItem != null || agent.FireRescueTarget != null ||
            agent.LastStandTarget != null || agent.PendingMeleeWeapon != null)
            return false;
        if (agent.PendingWeapon == null && HasCoverCommitment(uid, agent, now))
            return false;
        _guns.TryGetGun(uid, out var current);
        if (current.Owner.IsValid() && WeaponAmmo(current) == 0)
        {
            // Bypass an ordinary weapon commitment once on depletion, while retaining
            // failed-pickup/stow backoff so a blocked swap cannot become a handling loop.
            if (agent.LastEmptyWeapon != current.Owner)
                agent.NextWeaponChoice = now;
            agent.LastEmptyWeapon = current.Owner;
        }
        else
            agent.LastEmptyWeapon = null;
        if (agent.PendingWeapon is { } pending)
        {
            if (now < agent.WeaponSwitchAt)
                return true;
            agent.PendingWeapon = null;
            agent.NextWeaponChoice = now + TimeSpan.FromSeconds(3);
            // Revalidate possession and usefulness after freeing the wielding hand.
            if (!CarriedWeapons(uid).Contains(pending) || WeaponScore(uid, agent, pending) < 0 ||
                current.Owner.IsValid() && WeaponAmmo(current) > 0 && !LoadoutReadinessSafe(uid, agent, now) &&
                WeaponScore(uid, agent, pending) < WeaponScore(uid, agent, current))
                return false;
            var wasSlung = _inventory.TryGetSlotEntity(uid, SuitStorageSlot, out var slung) && slung == pending;
            if (!_hands.IsHolding(uid, pending, out _) && !_hands.TryPickupAnyHand(uid, pending))
                return false;
            if (current.Owner.IsValid() && current.Owner != pending && !StowWeapon(uid, current) &&
                HasComp<GunRequiresWieldComponent>(pending))
            {
                // Roll back to the source slot rather than discard a still-loaded primary.
                if (wasSlung)
                    _inventory.TryEquip(uid, pending, SuitStorageSlot, silent: true);
                else
                    StoreSupply(uid, pending);
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
        var prepare = LoadoutReadinessWeapon(uid, agent, now);
        if (prepare == current.Owner)
            return false;
        var best = current.Owner.IsValid() ? WeaponScore(uid, agent, current) + (WeaponAmmo(current) > 0 ? 4 : 0) : -1;
        EntityUid? chosen = prepare;
        foreach (var weapon in CarriedWeapons(uid))
        {
            if (prepare != null)
                break;
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

    private bool StowWeapon(EntityUid uid, EntityUid weapon)
    {
        _wield.TryUnwield(weapon, uid);
        // Only a spent disposable tube is deliberately discarded. Empty rifles remain useful
        // after resupply. Failed storage must not strand them on the floor during a swap.
        if (TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) && role.Rocket && WeaponAmmo(weapon) == 0)
            return _hands.TryDrop(uid, weapon);
        return _inventory.TryEquip(uid, weapon, SuitStorageSlot, silent: true) ||
            StoreSupply(uid, weapon);
    }

    private void StowOtherWeapons(EntityUid uid, EntityUid active)
    {
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var held) && held != active && HasComp<GunComponent>(held))
                StowWeapon(uid, held.Value);
    }

    private bool StoreOwnedItem(EntityUid uid, EntityUid item, Content.Shared.Storage.StorageComponent bag) =>
        _scavengeStorage.CanInsert(bag.Container.Owner, item, out _) &&
        _scavengeRmcStorage.CanInsert((bag.Container.Owner, bag), item, uid, out _) &&
        _scavengeStorage.Insert(bag.Container.Owner, item, out _, user: uid, stackAutomatically: false) && bag.Container.Contains(item);

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
        var loaded = WeaponAmmo(weapon) > 0;
        if (!loaded && SpareAmmunition(uid, weapon) == null)
            return -100;
        TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role);
        // Under contact, a loaded backup beats an empty primary. During a lull, restore
        // the preferred reloadable weapon instead of remaining on the pistol indefinitely.
        if (!loaded)
        {
            if (role?.Rocket == true)
                return -100;
            var quiet = agent.Target == null && _timing.CurTime >= agent.ForgetAt &&
                _timing.CurTime - agent.LastHit > TimeSpan.FromSeconds(2);
            // Once both guns are empty, reload the preferred primary instead of tying
            // every empty candidate with the currently held pistol. Every loaded gun
            // still outranks these contact-time scores (loaded scores are at least 2).
            return quiet ? role?.Priority ?? 20 : Math.Clamp((role?.Priority ?? 20) / 100f, 0.01f, 1f);
        }
        var score = role?.Priority ?? 20;
        if (agent.Target is not { } target || !Visible(uid, target, agent.FireRange))
            return role?.Rocket == true ? -100 : score;
        var point = Transform(target).Coordinates;
        var distance = Vector2.Distance(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(target));
        if (role != null)
        {
            if (!(role.Rocket && ArmedVehicle(target)) && (distance < role.MinimumRange || distance > role.MaximumRange))
            {
                if (role.Rocket)
                    return -100;
                // Select the pistol and manoeuvre into its range instead of refusing to draw it.
                score -= 8;
            }
            if (distance < role.CloseRange)
                score += role.ClosePriority;
            if (role.Rocket && (!RocketOpportunity(uid, agent, target, point) ||
                !SafeShot(uid, agent, (weapon, gun), point)))
                return -100;
        }
        return Math.Max(2, score);
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
        // A visible shooter actively wounding the squad warrants the tube too. Waiting
        // for two hits on this agent's own cover peek ignores open-ground ambushes and
        // the gunner already killing the front of the column.
        if (ArmedVehicle(target) && VehicleDisposition(uid, agent, target) > 0)
            return true;
        if (agent.RepeatedPeekHits >= 2 && _timing.CurTime - agent.LastPeekHit < TimeSpan.FromSeconds(15) ||
            agent.RecentShooters.TryGetValue(target, out var until) && _timing.CurTime < until)
            return true;
        var squad = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (squad.MoveNext(out var other, out var buddy))
            if (LocalSquadMember(uid, agent, other, buddy) &&
                buddy.RecentShooters.TryGetValue(target, out until) && _timing.CurTime < until)
                return true;
        var contacts = 0;
        foreach (var threat in agent.VisibleThreats)
            if (_transform.InRange(point, threat, 3) && ++contacts >= 2)
                return true;
        return false;
    }

    private bool SafeWeaponEffect(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun,
        EntityCoordinates start, EntityCoordinates destination)
    {
        if (!TryComp<CMUExpeditionWeaponRoleComponent>(gun.Owner, out var role))
            return true;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(destination);
        var distance = Vector2.Distance(from.Position, to.Position);
        var blastPoint = destination;
        var vehicle = role.Rocket ? VehicleAimBody(uid) : null;
        if (vehicle is { } hull)
        {
            // Range and explosion safety use the first hull impact, not the centre of a
            // large APC. A wall or another body before the hull invalidates this rocket.
            if (!VehicleImpact(uid, hull, start, destination, out blastPoint))
                return false;
            distance = Vector2.Distance(from.Position, _transform.ToMapCoordinates(blastPoint).Position);
        }
        if (distance < role.MinimumRange || distance > role.MaximumRange)
            return false;
        if (!role.Rocket)
            return true;
        if (_timing.CurTime < agent.NextRocket || !SafeBlast(uid, blastPoint, role.BlastRadius, 0.4f) ||
            !ClearLane(uid, start, destination, 0.6f, impactBody: vehicle))
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
