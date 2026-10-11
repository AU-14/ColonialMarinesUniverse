using System.Numerics;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void RememberReturnFire(EntityUid uid, CMUExpeditionAgentComponent agent,
        MapCoordinates observed, Vector2 incoming, TimeSpan now)
    {
        var origin = _transform.GetMapCoordinates(uid);
        if (incoming.LengthSquared() < 0.01f || observed.MapId != origin.MapId ||
            Transform(uid).MapUid is not { } map)
            return;
        var direction = -Vector2.Normalize(incoming);
        var point = observed.Offset(direction * Math.Min(6, WeaponFireRange(uid, agent)));
        // A recent directly seen contact is stronger evidence only if it agrees with
        // this incoming bearing. Never read the hidden shooter's current transform.
        if (!agent.ContactFromRadio && agent.LastSeen is { } last && Exists(last.EntityId) &&
            now - agent.LastContact < agent.ReturnFireMemory)
        {
            var contact = _transform.ToMapCoordinates(last);
            var offset = contact.Position - origin.Position;
            if (contact.MapId == origin.MapId && offset.LengthSquared() > 1 &&
                Vector2.Dot(Vector2.Normalize(offset), direction) > 0.85f)
                point = contact;
        }
        var side = new Vector2(-direction.Y, direction.X);
        point = point.Offset(side * _visionRandom.NextFloat(-0.6f, 0.6f));
        // Freeze one uncertain point for native attempt/revalidation callbacks.
        agent.ReturnFirePoint = _transform.ToCoordinates(map, point);
        agent.ReturnFireUntil = now + agent.ReturnFireMemory;
        if (now >= agent.ReturnFireWindowEnd)
        {
            agent.ReturnFireWindowEnd = now + agent.ReturnFireMemory;
            agent.ReturnFireRounds = agent.ReturnFireRoundLimit;
        }
    }

    private bool TryReturnFireAim(EntityUid uid, CMUExpeditionAgentComponent agent,
        Entity<GunComponent> gun, out EntityCoordinates point)
    {
        point = default;
        var now = _timing.CurTime;
        if (now >= agent.ReturnFireUntil || agent.ReturnFireRounds <= 0 ||
            agent.ReturnFirePoint is not { } remembered || !Exists(remembered.EntityId) ||
            TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role) && role.Rocket)
            return false;
        var origin = _transform.GetMapCoordinates(uid);
        var contact = _transform.ToMapCoordinates(remembered);
        var delta = contact.Position - origin.Position;
        if (contact.MapId != origin.MapId || delta.LengthSquared() < 1)
            return false;
        var range = WeaponFireRange(uid, agent);
        if (delta.LengthSquared() > range * range)
            contact = origin.Offset(Vector2.Normalize(delta) * range);
        point = _transform.ToCoordinates(Transform(uid).MapUid!.Value, contact);
        // Smoke obscures sight, not ballistics. Walls and the enlarged friendly cone
        // are still checked by SafeShot, including for the native trigger callback.
        return FiringLaneClear(uid, Transform(uid).Coordinates, point);
    }
}
