using Content.Shared.Doors.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Dropship;

[Serializable, NetSerializable]
public sealed class DropshipNavigationDestinationsBuiState(NetEntity? flyBy, List<Destination> destinations, Dictionary<DoorLocation, bool> doorLockStatus, bool remoteControlStatus, bool canTacticalLand, bool launchAlarmStatus, bool canWithdrawReturn = false, bool tacticalHoverActive = false, bool hasFirearm = false, bool firearmArmed = false, bool hasMasterAlarm = false, bool masterAlarmSilenced = false) : BoundUserInterfaceState // CMU14: firearm toggle + master alarm params
{
    public readonly NetEntity? FlyBy = flyBy;
    public readonly List<Destination> Destinations = destinations;
    public readonly Dictionary<DoorLocation, bool> DoorLockStatus = doorLockStatus;
    public readonly bool RemoteControlStatus = remoteControlStatus;
    public readonly bool CanTacticalLand = canTacticalLand;
    public readonly bool LaunchAlarmStatus = launchAlarmStatus;
    public readonly bool CanWithdrawReturn = canWithdrawReturn;
    public readonly bool TacticalHoverActive = tacticalHoverActive;
    // CMU14: gunship firearm fire-control toggle
    public readonly bool HasFirearm = hasFirearm;
    public readonly bool FirearmArmed = firearmArmed;
    // CMU14: master alarm mute state, only shown when the ship has hull integrity alarms
    public readonly bool HasMasterAlarm = hasMasterAlarm;
    public readonly bool MasterAlarmSilenced = masterAlarmSilenced;
}

[Serializable, NetSerializable]
public sealed class DropshipNavigationTravellingBuiState(FTLState state, StartEndTime time, string destination, string departureLocation, Dictionary<DoorLocation, bool> doorLockStatus, bool remoteControlStatus, bool launchAlarmStatus, bool hasMasterAlarm = false, bool masterAlarmSilenced = false) : BoundUserInterfaceState // CMU14: master alarm params
{
    public readonly FTLState State = state;
    public readonly StartEndTime Time = time;
    public readonly string Destination = destination;
    public readonly string DepartureLocation = departureLocation;
    public readonly Dictionary<DoorLocation, bool> DoorLockStatus = doorLockStatus;
    public readonly bool RemoteControlStatus = remoteControlStatus;
    public readonly bool LaunchAlarmStatus = launchAlarmStatus;
    // CMU14: master alarm mute state, only shown when the ship has hull integrity alarms
    public readonly bool HasMasterAlarm = hasMasterAlarm;
    public readonly bool MasterAlarmSilenced = masterAlarmSilenced;
}

[Serializable, NetSerializable]
public sealed class DropshipNavigationLaunchMsg(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class DropshipNavigationCancelMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DropshipLockdownMsg(DoorLocation doorLocation) : BoundUserInterfaceMessage
{
    public readonly DoorLocation DoorLocation = doorLocation;
}

[Serializable, NetSerializable]
public enum DropshipNavigationUiKey
{
    Key
}

[Serializable, NetSerializable]
public readonly record struct Destination(NetEntity Id, string Name, bool Occupied, bool Primary);

[Serializable, NetSerializable]
public sealed class DropshipRemoteControlToggleMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipLaunchAlarmToggleMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable] // CMU14: dropship master alarm toggle
public sealed class DropshipMasterAlarmToggleMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipWithdrawReturnMsg : BoundUserInterfaceMessage;
