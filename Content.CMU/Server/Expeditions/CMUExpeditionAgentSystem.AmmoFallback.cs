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
        if (agent.ScavengeTarget != null)
            agent.RifleLoweredUntil = TimeSpan.Zero;
        agent.ScavengeTarget = null;
        agent.ScavengeUntil = TimeSpan.Zero;
        agent.ScavengePickupAt = TimeSpan.Zero;
        if (agent.State == CMUExpeditionAgentState.Scavenge)
        {
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Guard;
        }
    }

    private bool RunAmmoFallback(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now, bool armed = false)
    {
        if (!armed)
            TryLastResortStrike(uid, agent);
        if (agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null ||
            agent.WorkItem != null || agent.PreparingWork || HasCoverCommitment(uid, agent, now) ||
            armed && (agent.OrderedDestination != null || agent.Target != null || now - agent.LastContact < TimeSpan.FromSeconds(8)) ||
            agent.RushTarget != null || GrenadeDanger(Transform(uid).Coordinates))
        {
            ClearScavenging(uid, agent);
            return false;
        }
        var start = Transform(uid).Coordinates;
        if (agent.ScavengeTarget is { } selected &&
            (now >= agent.ScavengeUntil ||
                agent.ScavengePickupAt != TimeSpan.Zero && agent.LastHit > agent.ScavengePickupAt ||
                !ScavengeSource(uid, selected, out var owner) || !UsefulLoot(uid, selected, armed) ||
                !_interaction.InRangeUnobstructed(uid, owner) && now - agent.LastHit < TimeSpan.FromSeconds(1)))
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
            foreach (var candidate in NearbyLoot(nearby.OrderBy(other =>
                         Vector2.DistanceSquared(map.Position, _transform.GetWorldPosition(other))))
                         .OrderBy(other => HasComp<GunComponent>(other) ? 2 : KnownLootGrenade(other, out _) ? 1 : 0))
            {
                if (!UsefulLoot(uid, candidate, armed) || !ScavengeSource(uid, candidate, out var source))
                    continue;
                if (!TrySquadCoordinates(Transform(source).Coordinates, out var point))
                    continue;
                var reachable = _interaction.InRangeUnobstructed(uid, source);
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
                agent.RifleLoweredUntil = TimeSpan.Zero;
                break;
            }
        }
        if (agent.ScavengeTarget is not { } found)
        {
            if (!armed)
                agent.WeaponDecision = "empty-seeking-supplies-or-shelter";
            return false;
        }
        if (!ScavengeSource(uid, found, out var lootSource) ||
            !TrySquadCoordinates(Transform(lootSource).Coordinates, out var lootPoint))
        {
            ClearScavenging(uid, agent);
            return false;
        }
        if (!_interaction.InRangeUnobstructed(uid, lootSource))
        {
            if (!TraversablePassage(uid, start, lootPoint) ||
                ExposureScore(uid, agent, lootPoint) > ExposureScore(uid, agent, start))
            {
                ClearScavenging(uid, agent);
                return false;
            }
            agent.State = CMUExpeditionAgentState.Scavenge;
            agent.ScavengePickupAt = TimeSpan.Zero;
            agent.RifleLoweredUntil = TimeSpan.Zero;
            agent.WeaponDecision = "retrieving-supplies";
            Move(uid, lootPoint, validated: true);
            return true;
        }
        _steering.Unregister(uid);
        if (agent.RifleLoweredUntil == TimeSpan.Zero)
        {
            if (_guns.TryGetGun(uid, out var held))
                _wield.TryUnwield(held.Owner, uid);
            agent.ScavengePickupAt = now;
            agent.RifleLoweredUntil = now + TimeSpan.FromSeconds(lootSource == found ? 0.25 : 0.8);
            return true;
        }
        if (now < agent.RifleLoweredUntil)
            return true;
        _guns.TryGetGun(uid, out var previous);
        if (!_hands.TryPickupAnyHand(uid, found))
        {
            ClearScavenging(uid, agent);
            return false;
        }
        if (!HasComp<GunComponent>(found))
        {
            if (!StoreScavengedSupply(uid, agent, found))
                _hands.TryDrop(uid, found);
            ClearScavenging(uid, agent);
            agent.NextScavenge = now + TimeSpan.FromSeconds(2);
            return true;
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
