using System.Linq;
using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._RMC14.Weapons.Melee;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedMeleeWeaponSystem _fallbackMelee = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _fallbackMeleeRange = default!;

    private bool ReloadPressureSafe(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        _mobs.IsAlive(uid) && !HasComp<ActorComponent>(uid) && agent.RushTarget == null &&
        agent.LastDamage < agent.EmergencyHealDamage &&
        _timing.CurTime - agent.LastHit >= TimeSpan.FromSeconds(1) && !GrenadeDanger(Transform(uid).Coordinates);

    private bool ReloadSafe(EntityUid uid, CMUExpeditionAgentComponent agent) =>
        TreatmentSafe(uid, agent) || ReloadPressureSafe(uid, agent) && agent.CoveringShooter != null &&
        ManeuverSupported(uid, agent, _timing.CurTime);

    private void TryLastResortStrike(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null ||
            agent.Target is not { } target || !Exists(target) || !_mobs.IsAlive(target) ||
            !AcceptOrderedContact(uid, agent, target) || !Visible(uid, target, 2) ||
            !_fallbackMelee.TryGetWeapon(uid, out var weapon, out var melee) ||
            melee.NextAttack > _timing.CurTime ||
            !_transform.InRange(Transform(uid).Coordinates, Transform(target).Coordinates,
                _fallbackMeleeRange.GetUserLightAttackRange(uid, target, melee)) ||
            !_interaction.InRangeUnobstructed(uid, target))
            return;
        // Use the held weapon's native butt/knife attack, or native unarmed combat. Never
        // chase into melee range just because ammunition is depleted; escape movement continues.
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combat.SetInCombatMode(uid, true, combat);
        if (_fallbackMelee.AttemptLightAttack(uid, weapon, melee, target))
        {
            agent.LastResortStrikes++;
            agent.WeaponDecision = "last-resort-melee";
        }
    }

    private void ClearScavenging(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        agent.ScavengeTarget = null;
        agent.ScavengeUntil = TimeSpan.Zero;
        if (agent.State == CMUExpeditionAgentState.Scavenge)
        {
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Guard;
        }
    }

    private bool ScavengeWeaponAvailable(EntityUid uid, EntityUid weapon) =>
        Exists(weapon) && HasComp<GunComponent>(weapon) && WeaponAmmo(weapon) > 0 &&
        !Transform(weapon).Anchored && !_containers.IsEntityOrParentInContainer(weapon) &&
        !(TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) && role.Rocket) &&
        Visible(uid, weapon, 4) && GroundSafe(Transform(weapon).Coordinates) && !GrenadeDanger(Transform(weapon).Coordinates);

    private bool RunAmmoFallback(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        TryLastResortStrike(uid, agent);
        if (agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null ||
            agent.RushTarget != null || GrenadeDanger(Transform(uid).Coordinates))
        {
            ClearScavenging(uid, agent);
            return false;
        }
        var start = Transform(uid).Coordinates;
        if (agent.ScavengeTarget is { } selected &&
            (now >= agent.ScavengeUntil || !ScavengeWeaponAvailable(uid, selected) ||
                !_interaction.InRangeUnobstructed(uid, selected) && now - agent.LastHit < TimeSpan.FromSeconds(1)))
        {
            ClearScavenging(uid, agent);
            agent.NextScavenge = now + TimeSpan.FromSeconds(2);
        }
        if (agent.ScavengeTarget == null && now >= agent.NextScavenge)
        {
            agent.NextScavenge = now + TimeSpan.FromSeconds(2);
            var nearby = new HashSet<EntityUid>();
            var map = _transform.GetMapCoordinates(uid);
            _lookup.GetEntitiesInRange(map.MapId, map.Position, 4, nearby);
            // Only loose physical guns, never inventory theft or fabricated ammunition.
            foreach (var candidate in nearby.Where(other => HasComp<GunComponent>(other))
                         .OrderBy(other => Vector2.DistanceSquared(map.Position, _transform.GetWorldPosition(other))).Take(12))
            {
                if (!ScavengeWeaponAvailable(uid, candidate))
                    continue;
                var point = Transform(candidate).Coordinates;
                var reachable = _interaction.InRangeUnobstructed(uid, candidate);
                if (!reachable && (now - agent.LastHit < TimeSpan.FromSeconds(1) ||
                    agent.Home is not { } home || !_transform.InRange(home, point, agent.LeashRange) ||
                    !TraversablePassage(uid, start, point) ||
                    ExposureScore(uid, agent, point) > ExposureScore(uid, agent, start)))
                    continue;
                var claimed = false;
                var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
                while (query.MoveNext(out var other, out var buddy))
                    claimed |= other != uid && buddy.ScavengeTarget == candidate && buddy.ScavengeUntil > now &&
                        _mobs.IsAlive(other) && !HasComp<ActorComponent>(other);
                if (claimed)
                    continue;
                agent.ScavengeTarget = candidate;
                agent.ScavengeUntil = now + TimeSpan.FromSeconds(5);
                agent.RifleLoweredUntil = now + TimeSpan.FromSeconds(0.3);
                if (_guns.TryGetGun(uid, out var empty))
                    _wield.TryUnwield(empty.Owner, uid);
                break;
            }
        }
        if (agent.ScavengeTarget is not { } found)
        {
            agent.WeaponDecision = "empty-seeking-supplies-or-shelter";
            return false;
        }
        if (!_interaction.InRangeUnobstructed(uid, found))
        {
            var point = Transform(found).Coordinates;
            if (!TraversablePassage(uid, start, point) ||
                ExposureScore(uid, agent, point) > ExposureScore(uid, agent, start))
            {
                ClearScavenging(uid, agent);
                return false;
            }
            agent.State = CMUExpeditionAgentState.Scavenge;
            agent.WeaponDecision = "retrieving-abandoned-weapon";
            Move(uid, point, validated: true);
            return true;
        }
        _steering.Unregister(uid);
        if (now < agent.RifleLoweredUntil)
            return true;
        _guns.TryGetGun(uid, out var previous);
        if (!_hands.TryPickupAnyHand(uid, found))
        {
            ClearScavenging(uid, agent);
            return false;
        }
        if (previous.Owner.IsValid() && previous.Owner != found && !StowWeapon(uid, previous))
            _hands.TryDrop(uid, previous.Owner);
        ActivateWeapon(uid, found);
        agent.Rifle = found;
        agent.WeaponsScavenged++;
        agent.WeaponDecision = "scavenged-weapon";
        ClearScavenging(uid, agent);
        ClearCover(agent);
        agent.NextWeaponChoice = now + TimeSpan.FromSeconds(2);
        Aim(agent, now, true);
        return true;
    }
}
