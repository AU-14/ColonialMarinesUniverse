using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ZLevels.Core;

internal static class CMUZLevelStairPreviewVisibility
{
    private const float DirectionEpsilon = 0.001f;
    private const float StairFootprintHalfExtent = 0.5f + DirectionEpsilon;

    public static bool IsInFrontOfStair(Vector2 viewerPosition, Vector2 stairPosition, Vector2 targetPosition)
    {
        var stairForward = stairPosition - viewerPosition;
        if (ViewerInsideStairFootprint(viewerPosition, stairPosition))
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
        return ProjectedCornersStayInFrontOfStair(viewerPosition, stairPosition,
            bounds.BottomLeft, bounds.TopLeft, bounds.TopRight, bounds.BottomRight, renderOffset);
    }

    public static bool ProjectedCornersStayInFrontOfStair(
        Vector2 viewerPosition,
        Vector2 stairPosition,
        Vector2 bottomLeft,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 renderOffset)
    {
        return IsInFrontOfStair(viewerPosition, stairPosition, bottomLeft - renderOffset) &&
               IsInFrontOfStair(viewerPosition, stairPosition, topLeft - renderOffset) &&
               IsInFrontOfStair(viewerPosition, stairPosition, topRight - renderOffset) &&
               IsInFrontOfStair(viewerPosition, stairPosition, bottomRight - renderOffset);
    }

    private static bool ViewerInsideStairFootprint(Vector2 viewerPosition, Vector2 stairPosition)
    {
        var delta = viewerPosition - stairPosition;
        return MathF.Abs(delta.X) <= StairFootprintHalfExtent &&
               MathF.Abs(delta.Y) <= StairFootprintHalfExtent;
    }
}
