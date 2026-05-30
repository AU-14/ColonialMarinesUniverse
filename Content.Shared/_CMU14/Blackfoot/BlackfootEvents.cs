using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Blackfoot;

[ByRefEvent]
public readonly record struct BlackfootStateChangedEvent(
    BlackfootFlightState OldState,
    BlackfootFlightState NewState);

[ByRefEvent]
public readonly record struct BlackfootAirborneChangedEvent(bool Airborne);

[ByRefEvent]
public readonly record struct BlackfootCrashedEvent;

public sealed partial class BlackfootEngineToggleActionEvent : InstantActionEvent;

public sealed partial class BlackfootTakeoffActionEvent : InstantActionEvent;

public sealed partial class BlackfootLandActionEvent : InstantActionEvent;

public sealed partial class BlackfootFlightModeToggleActionEvent : InstantActionEvent;

public sealed partial class BlackfootRearDoorToggleActionEvent : InstantActionEvent;

public sealed partial class BlackfootStowToggleActionEvent : InstantActionEvent;

public sealed partial class BlackfootAscendZLevelActionEvent : InstantActionEvent;

public sealed partial class BlackfootDescendZLevelActionEvent : InstantActionEvent;

public sealed partial class BlackfootDoorGunZModeToggleActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum BlackfootFlightComputerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BlackfootFlightComputerBuiState(
    NetEntity? aircraft,
    float fuel,
    float maxFuel,
    float battery,
    float maxBattery,
    bool refueling,
    bool recharging,
    bool padLinked,
    bool fuelPumpLinked) : BoundUserInterfaceState
{
    public readonly NetEntity? Aircraft = aircraft;
    public readonly float Fuel = fuel;
    public readonly float MaxFuel = maxFuel;
    public readonly float Battery = battery;
    public readonly float MaxBattery = maxBattery;
    public readonly bool Refueling = refueling;
    public readonly bool Recharging = recharging;
    public readonly bool PadLinked = padLinked;
    public readonly bool FuelPumpLinked = fuelPumpLinked;
}

[Serializable, NetSerializable]
public sealed class BlackfootFlightComputerFuelToggleMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BlackfootFlightComputerBatteryToggleMsg : BoundUserInterfaceMessage;
