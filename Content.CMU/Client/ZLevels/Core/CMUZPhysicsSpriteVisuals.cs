using System.Numerics;

namespace Content.Client._CMU14.ZLevels.Core;

internal static class CMUZPhysicsSpriteVisuals
{
    internal const float ActiveEpsilon = 0.001f;

    public static bool IsActive(float localPosition)
    {
        return MathF.Abs(localPosition) > ActiveEpsilon;
    }

    public static CMUZPhysicsSpriteState GetActiveState(
        CMUZPhysicsSpriteState baseline,
        float localPosition,
        float zLevelOffset,
        int elevatedDrawDepth)
    {
        return new CMUZPhysicsSpriteState(
            true,
            localPosition > 0f ? elevatedDrawDepth : baseline.DrawDepth,
            baseline.Offset + new Vector2(0f, localPosition * zLevelOffset));
    }

    public static CMUZPhysicsSpriteState RefreshBaseline(
        CMUZPhysicsSpriteState baseline,
        CMUZPhysicsSpriteState applied,
        CMUZPhysicsSpriteState current)
    {
        return new CMUZPhysicsSpriteState(
            current.NoRotation != applied.NoRotation ? current.NoRotation : baseline.NoRotation,
            current.DrawDepth != applied.DrawDepth ? current.DrawDepth : baseline.DrawDepth,
            current.Offset != applied.Offset ? current.Offset : baseline.Offset);
    }

    public static CMUZPhysicsSpriteState RestoreOwnedState(
        CMUZPhysicsSpriteState baseline,
        CMUZPhysicsSpriteState applied,
        CMUZPhysicsSpriteState current)
    {
        return new CMUZPhysicsSpriteState(
            current.NoRotation == applied.NoRotation ? baseline.NoRotation : current.NoRotation,
            current.DrawDepth == applied.DrawDepth ? baseline.DrawDepth : current.DrawDepth,
            current.Offset == applied.Offset ? baseline.Offset : current.Offset);
    }
}

internal readonly record struct CMUZPhysicsSpriteState(
    bool NoRotation,
    int DrawDepth,
    Vector2 Offset);
