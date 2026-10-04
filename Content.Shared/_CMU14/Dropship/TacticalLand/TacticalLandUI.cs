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
    bool tacticalHover,
    bool canMoveUp,
    bool canMoveDown,
    Dictionary<DoorLocation, bool> doorLockStatus,
    bool remoteControlStatus) : BoundUserInterfaceState
{
    public readonly NetEntity? Eye = eye;
    public readonly bool ClearForLanding = clearForLanding;
    public readonly bool TacticalHover = tacticalHover;
    public readonly bool CanMoveUp = canMoveUp;
    public readonly bool CanMoveDown = canMoveDown;
    public readonly Dictionary<DoorLocation, bool> DoorLockStatus = doorLockStatus;
    public readonly bool RemoteControlStatus = remoteControlStatus;
}

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandStartMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandConfirmMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandCancelMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandMoveUpMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalLandMoveDownMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipNavigationTacticalHoverCancelMsg : BoundUserInterfaceMessage;
