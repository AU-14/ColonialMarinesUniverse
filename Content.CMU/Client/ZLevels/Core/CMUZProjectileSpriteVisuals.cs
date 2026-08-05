using System.Numerics;

namespace Content.Client._CMU14.ZLevels.Core;

internal static class CMUZProjectileSpriteVisuals
{
    public static Vector2 Apply(
        Vector2 currentOffset,
        Vector2 targetVisualOffset,
        ref Vector2? originalOffset,
        ref Vector2 appliedOffset)
    {
        if (originalOffset is null ||
            currentOffset != originalOffset.Value + appliedOffset)
        {
            originalOffset = currentOffset - appliedOffset;
        }

        appliedOffset = targetVisualOffset;
        return originalOffset.Value + targetVisualOffset;
    }

    public static Vector2 Restore(
        Vector2 currentOffset,
        Vector2? originalOffset,
        Vector2 appliedOffset)
    {
        if (originalOffset is null)
            return currentOffset;

        return currentOffset == originalOffset.Value + appliedOffset
            ? originalOffset.Value
            : currentOffset - appliedOffset;
    }

    public static void TransferOwnership(
        Vector2? sourceOriginalOffset,
        Vector2 sourceAppliedOffset,
        ref Vector2? targetOriginalOffset,
        ref Vector2 targetAppliedOffset)
    {
        targetOriginalOffset = sourceOriginalOffset;
        targetAppliedOffset = sourceAppliedOffset;
    }
}
