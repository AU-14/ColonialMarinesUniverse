using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.ZLevels.Core;

internal static class CMUZLevelStairPreviewVisibility
{
    private const float DirectionEpsilon = 0.001f;

    public static bool IsInFrontOfStair(Vector2 viewerPosition, Vector2 stairPosition, Vector2 targetPosition)
    {
        var stairForward = stairPosition - viewerPosition;
        if (stairForward.LengthSquared() <= DirectionEpsilon)
            return true;

        var stairToTarget = targetPosition - stairPosition;
        return Vector2.Dot(stairForward, stairToTarget) >= -DirectionEpsilon;
    }

    public static bool ProjectedBoundsStayInFrontOfStair(
        Vector2 viewerPosition,
        Vector2 stairPosition,
        Box2 bounds,
        Vector2 renderOffset)
    {
        return IsInFrontOfStair(viewerPosition, stairPosition, bounds.BottomLeft - renderOffset) &&
               IsInFrontOfStair(viewerPosition, stairPosition, bounds.TopLeft - renderOffset) &&
               IsInFrontOfStair(viewerPosition, stairPosition, bounds.TopRight - renderOffset) &&
               IsInFrontOfStair(viewerPosition, stairPosition, bounds.BottomRight - renderOffset);
    }
}
