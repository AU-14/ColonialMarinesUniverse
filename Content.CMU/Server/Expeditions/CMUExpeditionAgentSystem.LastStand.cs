using System.Linq;
using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void ClearLastStand(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.DrawnMeleeWeapon is not { } item || !Exists(item) || HasComp<ActorComponent>(uid) ||
            !_hands.IsHolding(uid, item, out _) || StoreSupply(uid, item) || _hands.TryDrop(uid, item))
            agent.DrawnMeleeWeapon = null;
        if (agent.LastStandTarget != null)
            _steering.Unregister(uid);
        agent.LastStandTarget = null;
        agent.LastStandOrigin = null;
        agent.PendingMeleeWeapon = null;
    }

    private static bool PursuerPreventsEscape(float distance, float closingSpeed, float escapeSpeed, bool blocked) =>
        distance <= 2.75f && (blocked && distance <= 2 || closingSpeed > 0.35f && closingSpeed + 0.15f >= escapeSpeed);

    private bool RunLastStand(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var target = agent.RushTarget ?? agent.Target;
        if (agent.UtilityCleanupItem != null || agent.FireRescueTarget != null || agent.FlareItem != null || agent.Treatment != null ||
            agent.PendingWeapon != null || agent.AimedWeapon != null || agent.Action != null ||
            target is not { } enemy || !CombatTargetAlive(enemy) || !AcceptOrderedContact(uid, agent, enemy) ||
            !Visible(uid, enemy, 3.5f) || HasUsableCarriedAmmo(uid) || !GroundSafe(Transform(uid).Coordinates))
        {
            ClearLastStand(uid, agent);
            return false;
        }
        var start = Transform(uid).Coordinates;
        var offset = _transform.GetWorldPosition(enemy) - _transform.GetWorldPosition(uid);
        var distance = offset.Length();
        var toward = distance > 0.01f ? offset / distance : Vector2.UnitX;
        var closing = TryComp<PhysicsComponent>(enemy, out var body) ? -Vector2.Dot(body.LinearVelocity, toward) : 0;
        var escapeSpeed = TryComp<MovementSpeedModifierComponent>(uid, out var speed) ? speed.CurrentSprintSpeed : 2.5f;
        var blocked = agent.LastMoveFailed &&
                agent.State is CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo &&
                now - agent.MoveUntil < TimeSpan.FromSeconds(1.5) ||
            now < agent.SpacingUntil && agent.SpacingDecision == "trapped-returning-fire" ||
            agent.SpacingDestination != null && now - agent.MoveProgressAt > TimeSpan.FromSeconds(1) ||
            distance <= 1.3f && now - agent.LastHit < TimeSpan.FromSeconds(1);
        if (agent.LastStandTarget != enemy)
        {
            if (!PursuerPreventsEscape(distance, closing, escapeSpeed, blocked))
                return false;
            // Revoke the failed escape once, rather than letting spacing and weapon
            // recovery take the knife away again on every think.
            CancelAgentActivity(uid, agent, "exhausted-and-caught");
            agent.LastStandTarget = enemy;
            agent.LastStandOrigin = start;
            agent.State = CMUExpeditionAgentState.OutOfAmmo;
        }
        else if (distance > 3 || closing <= 0.1f && !blocked && now - agent.LastHit > TimeSpan.FromSeconds(2))
        {
            ClearLastStand(uid, agent);
            return false;
        }
        ReadyMeleeWeapon(uid, agent, now);
        if (!_fallbackMelee.TryGetWeapon(uid, out _, out var melee))
            return true;
        var reach = _fallbackMeleeRange.GetUserLightAttackRange(uid, enemy, melee);
        if (_transform.InRange(start, Transform(enemy).Coordinates, reach))
        {
            _steering.Unregister(uid);
            agent.Target = enemy;
            TryLastResortStrike(uid, agent);
        }
        else if (!agent.HoldPosition && agent.LastStandOrigin is { } origin)
        {
            // Only close the final short gap to the pursuer that already caught us.
            // This never turns an ammunition shortage into a long melee pursuit.
            var point = _transform.ToCoordinates(start.EntityId,
                _transform.GetMapCoordinates(uid).Offset(toward * Math.Min(0.75f, Math.Max(0, distance - reach * 0.85f))));
            if (_transform.InRange(origin, point, 1.5f) && agent.Home is { } home &&
                _transform.InRange(home, point, agent.LeashRange) &&
                TraversablePassage(uid, start, point, allowVault: false) && KnownDangerPassage(uid, agent, start, point))
                Move(uid, point, precise: true, validated: true);
            else
                _steering.Unregister(uid);
        }
        Decision(agent, "last-resort-melee", "out-of-ammo-and-unable-to-outpace-pursuer");
        return true;
    }

    private void ReadyMeleeWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.PendingMeleeWeapon is { } pending)
        {
            if (now < agent.MeleeWeaponReadyAt)
                return;
            agent.PendingMeleeWeapon = null;
            if (!Exists(pending) || !CarriedMeleeItems(uid).Contains(pending) ||
                !_hands.IsHolding(uid, pending, out _) && !_hands.TryPickupAnyHand(uid, pending))
                return;
            ActivateWeapon(uid, pending);
            agent.DrawnMeleeWeapon = pending;
            agent.WeaponDecision = "last-resort-melee-weapon";
            return;
        }
        if (now < agent.NextMeleeWeaponChoice)
            return;
        agent.NextMeleeWeaponChoice = now + TimeSpan.FromSeconds(2);
        _fallbackMelee.TryGetWeapon(uid, out var current, out _);
        var best = MeleeUtility(uid, current);
        EntityUid? chosen = null;
        foreach (var item in CarriedMeleeItems(uid))
        {
            if (HasComp<GunComponent>(item))
                continue;
            var score = MeleeUtility(uid, item);
            if (score <= best)
                continue;
            best = score;
            chosen = item;
        }
        if (chosen is not { } weapon)
            return;
        PrepareUtilityHand(uid);
        agent.PendingMeleeWeapon = weapon;
        agent.MeleeWeaponReadyAt = now + TimeSpan.FromSeconds(0.25);
    }

    private IEnumerable<EntityUid> CarriedMeleeItems(EntityUid uid)
    {
        foreach (var hand in _hands.EnumerateHands(uid))
            if (_hands.TryGetHeldItem(uid, hand, out var held))
                yield return held.Value;
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.MoveNext(out var slot))
            if (slot.ContainedEntity is { } item)
                yield return item;
        foreach (var item in SupplyItems(uid))
            yield return item;
    }

    private float MeleeUtility(EntityUid uid, EntityUid item) =>
        item.IsValid() && TryComp<MeleeWeaponComponent>(item, out var melee) && !melee.MustBeEquippedToUse
            ? _fallbackMelee.GetDamage(item, uid, melee).GetTotal().Float() * _fallbackMelee.GetAttackRate(item, uid, melee)
            : 0;
}
