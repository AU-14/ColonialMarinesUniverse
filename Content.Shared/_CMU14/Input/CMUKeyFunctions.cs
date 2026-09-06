using Robust.Shared.Input;

namespace Content.Shared._CMU14.Input;

[KeyFunctions]
public sealed class CMUKeyFunctions
{
    public static readonly BoundKeyFunction CMUCycleBodyZoneTarget = "CMUCycleBodyZoneTarget";
    public static readonly BoundKeyFunction CMUCycleBodyZoneTargetReverse = "CMUCycleBodyZoneTargetReverse";
    public static readonly BoundKeyFunction CMUTargetBodyZoneHead = "CMUTargetBodyZoneHead";
    public static readonly BoundKeyFunction CMUTargetBodyZoneTorso = "CMUTargetBodyZoneTorso";
    public static readonly BoundKeyFunction CMUTargetBodyZoneLeftArm = "CMUTargetBodyZoneLeftArm";
    public static readonly BoundKeyFunction CMUTargetBodyZoneRightArm = "CMUTargetBodyZoneRightArm";
    public static readonly BoundKeyFunction CMUTargetBodyZoneLeftLeg = "CMUTargetBodyZoneLeftLeg";
    public static readonly BoundKeyFunction CMUTargetBodyZoneRightLeg = "CMUTargetBodyZoneRightLeg";
    public static readonly BoundKeyFunction CMUInspectInjuries = "CMUInspectInjuries";
    public static readonly BoundKeyFunction CMUOpenMedicalCraftingMenu = "CMUOpenMedicalCraftingMenu";
    public static readonly BoundKeyFunction CMUToggleShootDownZLevel = "CMUToggleShootDownZLevel";

    public static readonly BoundKeyFunction CMUGunshipForward = "CMUGunshipForward";
    public static readonly BoundKeyFunction CMUGunshipBack = "CMUGunshipBack";
    public static readonly BoundKeyFunction CMUGunshipLeft = "CMUGunshipLeft";
    public static readonly BoundKeyFunction CMUGunshipRight = "CMUGunshipRight";
    public static readonly BoundKeyFunction CMUGunshipRotateLeft = "CMUGunshipRotateLeft";
    public static readonly BoundKeyFunction CMUGunshipRotateRight = "CMUGunshipRotateRight";
    public static readonly BoundKeyFunction CMUGunshipAscend = "CMUGunshipAscend";
    public static readonly BoundKeyFunction CMUGunshipDescend = "CMUGunshipDescend";
    // Retained as inert key-function IDs so existing player keybind files still deserialize cleanly.
    // The settings entries and input handlers were removed in favor of HUD camera previews.
    public static readonly BoundKeyFunction CMUGunshipViewUp = "CMUGunshipViewUp";
    public static readonly BoundKeyFunction CMUGunshipViewDown = "CMUGunshipViewDown";
    public static readonly BoundKeyFunction CMUGunshipRearView = "CMUGunshipRearView";
    public static readonly BoundKeyFunction CMUGunshipIncreaseThrust = "CMUGunshipIncreaseThrust";
    public static readonly BoundKeyFunction CMUGunshipDecreaseThrust = "CMUGunshipDecreaseThrust";
    public static readonly BoundKeyFunction CMUGunshipCycleCamera = "CMUGunshipCycleCamera";
    public static readonly BoundKeyFunction CMUGunshipTogglePanning = "CMUGunshipTogglePanning";

    // Emote keybinds - which emote each one plays is configured in the keybinds tab.
    public static readonly BoundKeyFunction CMUEmoteSlot1 = "CMUEmoteSlot1";
    public static readonly BoundKeyFunction CMUEmoteSlot2 = "CMUEmoteSlot2";
    public static readonly BoundKeyFunction CMUEmoteSlot3 = "CMUEmoteSlot3";
    public static readonly BoundKeyFunction CMUEmoteSlot4 = "CMUEmoteSlot4";
    public static readonly BoundKeyFunction CMUEmoteSlot5 = "CMUEmoteSlot5";
    public static readonly BoundKeyFunction CMUEmoteSlot6 = "CMUEmoteSlot6";
    public static readonly BoundKeyFunction CMUEmoteSlot7 = "CMUEmoteSlot7";
    public static readonly BoundKeyFunction CMUEmoteSlot8 = "CMUEmoteSlot8";
}
