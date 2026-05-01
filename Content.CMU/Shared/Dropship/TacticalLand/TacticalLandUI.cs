using Content.Shared.Doors.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

/// <summary>
///     UI state shown to a pilot who is currently controlling the tactical-land eye.
///     Replaces the destinations panel for that user only.
/// </summary>
[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandBuiState(
    NetEntity? eye,
    bool clearForLanding,
    Dictionary<DoorLocation, bool> doorLockStatus,
    bool remoteControlStatus) : BoundUserInterfaceState
{
    public readonly NetEntity? Eye = eye;
    public readonly bool ClearForLanding = clearForLanding;
    public readonly Dictionary<DoorLocation, bool> DoorLockStatus = doorLockStatus;
    public readonly bool RemoteControlStatus = remoteControlStatus;
}

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandStartMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandConfirmMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandCancelMsg : BoundUserInterfaceMessage;
