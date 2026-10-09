using System.Numerics;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private readonly List<(EntityCoordinates Point, TimeSpan Until)> _smokeScreens = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), bool> _smokeTiles = new();

    private bool HasGrenadeContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates target)
    {
        foreach (var hostile in ExpeditionHostiles(uid, agent))
            if (_mobs.IsAlive(hostile) && _transform.InRange(Transform(hostile).Coordinates, target, 3) &&
                Visible(uid, hostile, agent.DetectionRange))
                return true;
        return false;
    }

    private EntityCoordinates? BlastPoint(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid grenade)
    {
        var opening = _timing.CurTime - agent.FirstContact < TimeSpan.FromSeconds(12);
        var desperate = agent.RepeatedPeekHits >= 2 || agent.Stress >= 0.8f ||
            !_guns.TryGetGun(uid, out var gun) || WeaponAmmo(gun) == 0;
        if ((!opening && !desperate) || agent.RushTarget != null)
            return null;
        EntityCoordinates? best = null;
        var bestScore = float.MinValue;
        // At most eight directional representatives; cluster membership only uses visible contacts.
        foreach (var threat in agent.ThreatSectors)
        {
            if (threat is not { } contact)
                continue;
            var point = contact.Position;
            var velocity = TryComp<PhysicsComponent>(contact.Target, out var body) ? body.LinearVelocity : Vector2.Zero;
            var lead = velocity * 0.5f;
            if (lead.LengthSquared() > 2.25f)
                lead = Vector2.Normalize(lead) * 1.5f;
            point = _transform.ToCoordinates(point.EntityId, _transform.ToMapCoordinates(point).Offset(lead));
            var count = 0;
            foreach (var visible in agent.VisibleThreats)
                if (_transform.InRange(visible, point, 3))
                    count++;
            var score = count * 3 - velocity.Length();
            if ((count < 2 && !desperate) || count == 0 || score <= bestScore ||
                GrenadeDanger(point) || !SafeGrenade(uid, point, grenade))
                continue;
            best = point;
            bestScore = score;
        }
        return best;
    }

    private EntityCoordinates? SmokePoint(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid grenade,
        EntityCoordinates protectedPoint)
    {
        // A screen conceals charging aliens as effectively as it conceals us. Do not
        // blind the squad while a visible/recent melee threat can close through it.
        if (agent.MeleeThreats.Count > 0 || agent.LastContactWasMelee ||
            agent.LastSeen is not { } contact || _timing.CurTime - agent.LastContact > TimeSpan.FromSeconds(2))
            return null;
        var origin = _transform.ToMapCoordinates(protectedPoint);
        var delta = _transform.ToMapCoordinates(contact).Position - origin.Position;
        if (delta.LengthSquared() < 9)
            return null;
        // Screen the hostile side of the withdrawal/casualty, rather than engulfing the whole squad.
        var direction = Vector2.Normalize(delta);
        foreach (var distance in new[] { Math.Min(4, delta.Length() * 0.5f), 2f })
        {
            var point = _transform.ToCoordinates(protectedPoint.EntityId, origin.Offset(direction * distance));
            if (!SafeGrenade(uid, point, grenade) || SmokeReserved(uid, agent, point))
                continue;
            return point;
        }
        return null;
    }

    private bool SmokeReserved(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point)
    {
        foreach (var screen in _smokeScreens)
            if (screen.Until > _timing.CurTime && _transform.InRange(screen.Point, point, 5))
                return true;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
            if (other != uid && SameSquad(uid, agent, other, buddy) && buddy.SmokeGrenade &&
                buddy.GrenadeReservationUntil > _timing.CurTime && buddy.GrenadeTarget is { } reserved &&
                _transform.InRange(reserved, point, 5))
                return true;
        return false;
    }

    private bool SmokeOccludes(EntityCoordinates from, EntityCoordinates to)
    {
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        var distance = Vector2.Distance(start.Position, end.Position);
        if (start.MapId != end.MapId || distance < 1.25f)
            return false;
        var steps = (int) Math.Ceiling(distance * 2);
        for (var sample = 0; sample <= steps; sample++)
        {
            var point = _transform.ToCoordinates(new MapCoordinates(
                Vector2.Lerp(start.Position, end.Position, sample / (float) steps), start.MapId));
            if (!_turf.TryGetTileRef(point, out var tile) || !TryComp<MapGridComponent>(tile.Value.GridUid, out var grid))
                continue;
            var key = (tile.Value.GridUid, tile.Value.GridIndices);
            if (!_smokeTiles.TryGetValue(key, out var opaque))
            {
                var anchored = _maps.GetAnchoredEntitiesEnumerator(key.GridUid, grid, key.GridIndices);
                while (anchored.MoveNext(out var entity))
                    if (HasComp<SmokeComponent>(entity) && TryComp<OccluderComponent>(entity, out var occluder) && occluder.Enabled)
                        opaque = true;
                _smokeTiles[key] = opaque;
            }
            if (opaque)
                return true;
        }
        return false;
    }
}
