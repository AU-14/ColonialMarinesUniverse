using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared._CMU14.Yautja;

public static class YautjaWallVisionTargeting
{
    public static bool IsActiveSource(
        bool visorIsEquipped,
        bool thermalVisionEnabled,
        bool visorOwnedByViewer,
        bool visorLinkedToMask,
        bool maskVisorEnabled)
    {
        return visorIsEquipped &&
               thermalVisionEnabled &&
               visorOwnedByViewer &&
               visorLinkedToMask &&
               maskVisorEnabled;
    }

    public static bool IsEligible(
        EntityUid viewer,
        EntityUid target,
        MapId viewerMap,
        MapId targetMap,
        bool targetIsMob,
        bool targetSpriteVisible,
        bool targetInContainer,
        bool thermalVisionEnabled)
    {
        return viewer != target &&
               viewerMap == targetMap &&
               thermalVisionEnabled &&
               targetIsMob &&
               targetSpriteVisible &&
               !targetInContainer;
    }
}
