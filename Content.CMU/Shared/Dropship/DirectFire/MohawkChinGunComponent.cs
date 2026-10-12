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
