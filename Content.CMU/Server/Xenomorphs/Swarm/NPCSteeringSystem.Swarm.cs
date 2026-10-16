using System.Numerics;
using Content.Server.CMU14.Xenomorphs.Swarm;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.NPC;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem
{
    private bool AccumulateSwarmSeparation(EntityUid uid, EntityUid other, Vector2 worldPos,
        ref Vector2 separation, ref float proximity)
    {
        if (uid == other || !IsSwarmLesser(uid) || !IsSwarmLesser(other))
            return false;

        if (TryComp<CMUSwarmTraversalComponent>(uid, out var traversal) && traversal.Crossing)
            return true;

        const float spacing = 0.9f;
        var toward = _transform.GetWorldPosition(other) - worldPos;
        var distance = toward.Length();
        if (distance >= spacing)
            return true;

        // Coincident pairs need opposite escape directions, so they separate instead
        // of drifting together along the same quantized steering direction.
        if (distance < 0.001f)
        {
            var first = uid.GetHashCode();
            var second = other.GetHashCode();
            var phase = unchecked((uint) (Math.Min(first, second) * 73856093 ^ Math.Max(first, second) * 19349663)) % 65536;
            toward = new Angle(phase * MathF.Tau / 65536f).ToVec();
            if (first > second)
                toward = -toward;
        }
        else
            toward /= distance;

        var weight = 1f - distance / spacing;
        separation -= toward * weight;
        proximity = MathF.Max(proximity, weight);
        return true;
    }

    private void ApplySwarmSeparation(Angle offsetRot, Vector2 separation, float proximity, Span<float> interest)
    {
        if (separation.LengthSquared() < 0.000001f)
            return;

        var away = offsetRot.RotateVec(Vector2.Normalize(separation));
        var weight = Math.Clamp(proximity * 2f, 0f, 1f);
        // Blend once for the entire neighborhood. Adding one seek map per neighbor
        // saturates every direction in a dense group and erases the escape direction.
        for (var i = 0; i < InterestDirections; i++)
            interest[i] = interest[i] * (1f - weight) + MathF.Max(0f, Vector2.Dot(away, Directions[i])) * weight;
    }

    private bool IsSwarmLesser(EntityUid uid)
    {
        return HasComp<CMUSwarmLesserComponent>(uid);
    }

    private bool IsSwarmPassable(EntityUid uid, EntityUid obstacle)
    {
        // Steering must agree with the swarm's physical collision rules, including
        // anchored queens and moving lessers sharing a narrow passage.
        return IsSwarmLesser(uid) && (HasComp<XenoComponent>(obstacle) || HasComp<ActiveNPCComponent>(obstacle));
    }
}
