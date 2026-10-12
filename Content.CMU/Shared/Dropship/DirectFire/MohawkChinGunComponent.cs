using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Dropship.DirectFire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MohawkChinGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 30f;

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
    public float CloseDefenseRange = 4f;
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
    public TimeSpan FreeFireUntil;
    public TimeSpan FreeFireReadyAt;
    public EntityUid? FreeFireConsole;
    public EntityUid? AutomaticTarget;
    [DataField, AutoNetworkedField]
    public float MaxHealth = 2000;
    [DataField, AutoNetworkedField]
    public bool Disabled;
    [DataField, AutoNetworkedField]
    public MohawkChinGunRepairStage RepairStage;
    [DataField]
    public float SlewDegreesPerSecond = 30f;
    [DataField]
    public TimeSpan AuthorizationDuration = TimeSpan.FromSeconds(30);
    public TimeSpan AuthorizedUntil;
    public EntityCoordinates? PendingManualShot;
    public float DesiredAimDegrees;
    public EntityUid? Eye;
    public EntityUid? PreviousEye;
    public bool PreviousDrawFov;
    public EntityUid? ControlledWeapon;
    public bool AddedRemoteOperator;
    public EntityUid? PreviousPlatform;
    public TimeSpan NextScan;
    public string? AssignedFaction;
}

[Serializable, NetSerializable]
public sealed class MohawkChinGunControlMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MohawkChinGunFreeFireMessage : BoundUserInterfaceMessage;

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
