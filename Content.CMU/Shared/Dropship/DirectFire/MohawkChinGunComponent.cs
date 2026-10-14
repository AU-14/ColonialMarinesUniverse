using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Dropship.DirectFire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MohawkChinGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 20f;

    [DataField, AutoNetworkedField]
    public EntityUid? Operator;

    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public EntityUid? TrackedTarget;

    [DataField, AutoNetworkedField]
    public bool TrackingFireConfirmed;
    [DataField, AutoNetworkedField]
    public MohawkTargetPriority TargetPriority;
    [DataField]
    public float CloseDefenseRange = 8f;
    [DataField]
    public float FlareRadius = 10f;
    public Vector2 CameraPan;
    public bool DefensiveFire;
    public TimeSpan NextAimUpdate;
    [DataField, AutoNetworkedField]
    public int ActiveFeed;
    [DataField, AutoNetworkedField]
    public bool AlternateFeeds;
    public EntityCoordinates? HeldAim;
    public TimeSpan HeldFireUntil;
    public bool AirborneControl;
    public TimeSpan NextFireWarning;
    public TimeSpan FreeFireReadyAt;
    public EntityUid? FreeFireConsole;
    public EntityUid? AutomaticTarget;
    [DataField, AutoNetworkedField] public bool Calibrated;
    [DataField, AutoNetworkedField] public bool IffConfigured;
    [DataField, AutoNetworkedField] public bool IffEnabled = true;
    [DataField, AutoNetworkedField] public bool FreeFireEnabled;
    public EntityUid? ApprovedAutomaticTarget;
    public EntityUid? AutomaticEye;
    public Vector2 KeyboardPan;
    public TimeSpan KeyboardPanUntil;
    [DataField, AutoNetworkedField]
    public bool ArMode;
    [DataField, AutoNetworkedField]
    public float MaxHealth = 2000;
    [DataField, AutoNetworkedField]
    public bool Disabled;
    [DataField, AutoNetworkedField]
    public MohawkChinGunRepairStage RepairStage;
    [DataField]
    public float SlewDegreesPerSecond = 60f;
    [DataField]
    public TimeSpan AuthorizationDuration = TimeSpan.FromSeconds(30);
    public TimeSpan AuthorizedUntil;
    public EntityCoordinates? PendingManualShot;
    public float DesiredAimDegrees;
    public EntityUid? Eye;
    public EntityUid? PreviousEye;
    public bool PreviousDrawFov;
    public bool PreviousDrawLight = true;
    [DataField, AutoNetworkedField]
    public MohawkChinGunVisionMode VisionMode;
    public EntityUid? ControlledWeapon;
    public bool AddedRemoteOperator;
    public EntityUid? PreviousPlatform;
    public TimeSpan NextScan;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunControlMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunFreeFireMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunArMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunIffMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunVisionMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum MohawkChinGunVisionMode : byte
{
    Normal,
    Night,

    /// <summary>Night vision that also ignores walls, even while grounded.</summary>
    Thermal,
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunApproveAiMessage(NetEntity? target) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Target = target;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunInputEvent(NetEntity console, Vector2 movement, NetCoordinates? aim,
    bool held, bool toggleAr = false) : EntityEventArgs
{
    public readonly NetEntity Console = console;
    public readonly Vector2 Movement = movement;
    public readonly NetCoordinates? Aim = aim;
    public readonly bool Held = held;
    public readonly bool ToggleAr = toggleAr;
}

[ByRefEvent]
public readonly record struct MohawkChinGunFlareLockedEvent(EntityUid Flare, float Radius);

[Serializable, NetSerializable]
public sealed class MohawkChinGunFireMessage(NetCoordinates coordinates) : BoundUserInterfaceMessage
{
    public NetCoordinates Coordinates = coordinates;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunTrackMessage(NetCoordinates coordinates) : BoundUserInterfaceMessage
{
    public readonly NetCoordinates Coordinates = coordinates;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunConfirmTrackingMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunClearTrackingMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum MohawkTargetPriority : byte
{
    Distance,
    HighestHealth,
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunAimMessage(NetCoordinates coordinates) : BoundUserInterfaceMessage
{
    public readonly NetCoordinates Coordinates = coordinates;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunPanMessage(Direction? direction) : BoundUserInterfaceMessage
{
    public readonly Direction? Direction = direction;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunPriorityMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunAcquireMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunFlareMessage(NetEntity? flare) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Flare = flare;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunTriggerMessage(bool held, NetCoordinates? coordinates = null) : BoundUserInterfaceMessage
{
    public readonly bool Held = held;
    public readonly NetCoordinates? Coordinates = coordinates;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunFeedMessage(int? feed = null) : BoundUserInterfaceMessage
{
    // Null toggles selected/alternating feed mode.
    public readonly int? Feed = feed;
}
