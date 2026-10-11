using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.NPC.Components;
using Content.Shared.Physics;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool VehicleBody(EntityUid target) => HasComp<VehicleEnterComponent>(target) || HasComp<GridVehicleMoverComponent>(target);

    private bool ArmedVehicle(EntityUid target) => Exists(target) && VehicleBody(target) &&
        !HasComp<VehicleTotaledComponent>(target) && !HasComp<ActiveTankCookOffComponent>(target) && MountedGun(target, 2);

    private bool MountedGun(EntityUid owner, int depth)
    {
        if (HasComp<GunComponent>(owner))
            return true;
        if (depth == 0 || !TryComp<HardpointSlotsComponent>(owner, out var mounts))
            return false;
        foreach (var slot in mounts.Slots)
            if (_itemSlots.TryGetSlot(owner, slot.Id, out var holder) && holder.Item is { } item && MountedGun(item, depth - 1))
                return true;
        return false;
    }

    private bool CombatTargetAlive(EntityUid target) => _mobs.IsAlive(target) || ArmedVehicle(target);

    // Vehicles have no MobState and often inherit their identity from operators/interiors.
    // Any friendly aboard vetoes a rocket; unknown/neutral vehicles are never invented enemies.
    private int VehicleDisposition(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid vehicle)
    {
        var identities = new HashSet<EntityUid> { vehicle };
        if (TryComp<VehicleComponent>(vehicle, out var driven) && driven.Operator is { } driver)
            identities.Add(driver);
        if (TryComp<VehicleWeaponsComponent>(vehicle, out var weapons))
        {
            if (weapons.Operator is { } gunner)
                identities.Add(gunner);
            foreach (var crew in weapons.HardpointOperators.Values)
                identities.Add(crew);
        }
        if (TryComp<VehicleInteriorComponent>(vehicle, out var interior))
            foreach (var crew in interior.Passengers)
                if (_mobs.IsAlive(crew))
                    identities.Add(crew);
        var factions = new HashSet<string>();
        foreach (var identity in identities)
            if (TryComp<NpcFactionMemberComponent>(identity, out var member))
                foreach (var faction in member.Factions)
                    factions.Add(faction.Id);
        if (TryComp<VehicleEnterComponent>(vehicle, out var entrance) && entrance.InteriorFaction is { } ownerFaction)
            factions.Add(ownerFaction.ToUpperInvariant());
        if (!TryComp<NpcFactionMemberComponent>(uid, out var own))
            return 0;
        if (factions.Any(faction => agent.FriendlyFactions.Contains(faction) ||
                own.Factions.Any(id => id.Id == faction) || own.FriendlyFactions.Any(id => id.Id == faction)))
            return -1;
        return factions.Any(faction => agent.TargetFactions.Contains(faction) || agent.TargetFactions.Count == 0 &&
            own.HostileFactions.Any(id => id.Id == faction)) ? 1 : 0;
    }

    private IEnumerable<EntityUid> HostileVehicles(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, agent.DetectionRange, nearby);
        foreach (var vehicle in nearby)
            if (ArmedVehicle(vehicle) && VehicleDisposition(uid, agent, vehicle) > 0)
                yield return vehicle;
    }

    private bool HasReadyRocket(EntityUid uid) => CarriedWeapons(uid).Any(weapon =>
        TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) && role.Rocket && WeaponAmmo(weapon) > 0);

    private EntityUid? VehicleAimBody(EntityUid uid) => TryComp<CMUExpeditionAgentComponent>(uid, out var agent) &&
        agent.Target is { } target && VehicleBody(target) && !agent.FiringAtFlash ? target : null;

    private bool VehicleImpact(EntityUid uid, EntityUid vehicle, EntityCoordinates start, EntityCoordinates aim,
        out EntityCoordinates impact)
    {
        impact = aim;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(aim);
        var delta = to.Position - from.Position;
        if (from.MapId != to.MapId || delta.LengthSquared() < 0.01f)
            return false;
        var ray = new CollisionRay(from.Position, Vector2.Normalize(delta),
            // APC, Humvee and tank hulls use LargeMobLayer instead of wall/bullet layers.
            // Include it in the first-impact query so a real hull is hittable and a
            // nearer vehicle cannot be mistaken for an unobstructed launch lane.
            (int) (CollisionGroup.BulletImpassable | CollisionGroup.Impassable | CollisionGroup.InteractImpassable |
                CollisionGroup.LargeMobLayer));
        foreach (var hit in _physics.IntersectRayWithPredicate(from.MapId, ray, delta.Length(),
                     entity => entity == uid, returnOnFirstHit: true))
        {
            if (hit.HitEntity != vehicle)
                return false;
            impact = _transform.ToCoordinates(start.EntityId, new MapCoordinates(hit.HitPos, from.MapId));
            return true;
        }
        return false;
    }
}
