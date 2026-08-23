using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private TurfSystem _turf = default!;

    public bool TryPreparePulledEntityZMove(EntityUid user, MapCoordinates oldUserCoordinates, out CMUZPulledEntityMove pulledMove)
    {
        pulledMove = default;

        if (!TryComp<PullerComponent>(user, out var puller) ||
            puller.Pulling is not { } pulled ||
            !TryComp<PullableComponent>(pulled, out var pullable) ||
            pullable.Puller != user)
        {
            return false;
        }

        var pulledCoordinates = _transform.GetMapCoordinates(pulled);
        if (pulledCoordinates.MapId != oldUserCoordinates.MapId)
            return false;

        var relativePosition = pulledCoordinates.Position - oldUserCoordinates.Position;
        pulledMove = new CMUZPulledEntityMove(user, pulled, relativePosition);
        return _pulling.TryStopPull(pulled, pullable, user);
    }

    public void MovePulledEntityAcrossZ(CMUZPulledEntityMove pulledMove, MapCoordinates targetUserCoordinates, float landingLocalPosition)
    {
        if (!pulledMove.Puller.IsValid() ||
            Deleted(pulledMove.Puller))
        {
            return;
        }

        if (!pulledMove.Pulled.IsValid() ||
            Deleted(pulledMove.Pulled))
        {
            return;
        }

        var targetPulledCoordinates = GetPulledLandingCoordinates(pulledMove, targetUserCoordinates);
        _transform.SetMapCoordinates(pulledMove.Pulled, targetPulledCoordinates);

        if (TryComp<CMUZPhysicsComponent>(pulledMove.Pulled, out var zPhysics))
        {
            SetZVelocity((pulledMove.Pulled, zPhysics), 0f);
            SetZLocalPosition((pulledMove.Pulled, zPhysics), landingLocalPosition);
        }

        _pulling.TryStartPull(pulledMove.Puller, pulledMove.Pulled);
    }

    private MapCoordinates GetPulledLandingCoordinates(CMUZPulledEntityMove pulledMove, MapCoordinates targetUserCoordinates)
    {
        var preferred = targetUserCoordinates.Position + pulledMove.RelativePosition;
        if (IsSafePulledLanding(new MapCoordinates(preferred, targetUserCoordinates.MapId)))
            return new MapCoordinates(preferred, targetUserCoordinates.MapId);

        var normalizedRelative = pulledMove.RelativePosition.LengthSquared() > 0.01f
            ? Vector2.Normalize(pulledMove.RelativePosition)
            : Vector2.Zero;

        Span<Vector2> candidateOffsets =
        [
            normalizedRelative,
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, 1f),
            new Vector2(1f, -1f),
            new Vector2(-1f, -1f),
        ];

        foreach (var offset in candidateOffsets)
        {
            if (offset == Vector2.Zero)
                continue;

            var candidate = new MapCoordinates(targetUserCoordinates.Position + offset, targetUserCoordinates.MapId);
            if (IsSafePulledLanding(candidate))
                return candidate;
        }

        return targetUserCoordinates;
    }

    private bool IsSafePulledLanding(MapCoordinates coordinates)
    {
        if (!_turf.TryGetTileRef(_transform.ToCoordinates(coordinates), out var tileRef))
            return false;

        return !tileRef.Value.Tile.IsEmpty &&
               !_turf.IsTileBlocked(tileRef.Value, CollisionGroup.MobMask);
    }
}

public readonly record struct CMUZPulledEntityMove(EntityUid Puller, EntityUid Pulled, Vector2 RelativePosition);
