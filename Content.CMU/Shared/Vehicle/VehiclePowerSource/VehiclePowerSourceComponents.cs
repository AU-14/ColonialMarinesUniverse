using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehiclePowerSourceTankComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Fuel = 2000f;

    [DataField, AutoNetworkedField]
    public float MaxFuel = 2000f;

    [DataField, AutoNetworkedField]
    public float Battery = 1000f;

    [DataField, AutoNetworkedField]
    public float MaxBattery = 1000f;

    [DataField, AutoNetworkedField]
    public float MinimumTakeoffFuel = 30f;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehiclePowerSourceDrainComponent : Component
{
    [DataField, AutoNetworkedField]
    public float NoFuelDrain = 0f;

    [DataField, AutoNetworkedField]
    public float IdleFuelDrain = 0.5f;

    [DataField, AutoNetworkedField]
    public float ReducedFuelDrain = 0.8f;

    [DataField, AutoNetworkedField]
    public float ActiveFuelDrain = 1f;

    [DataField, AutoNetworkedField]
    public float OverdriveFuelDrain = 3f;

    [DataField, AutoNetworkedField]
    public float SensorBatteryDrain = 1f;

    [DataField, AutoNetworkedField]
    public float MinimumTakeoffFuel = 30f;

    [DataField, AutoNetworkedField]
    public float LeakNormal = 1f;

    [DataField, AutoNetworkedField]
    public float LeakPatched = 1.05f;

    [DataField, AutoNetworkedField]
    public float LeakLeaking = 1.5f;

    [DataField, AutoNetworkedField]
    public float LeakTorn = 5f;

    [DataField, AutoNetworkedField]
    public float LeakRuptured = 20f;

    [DataField, AutoNetworkedField]
    public float LeakGone = 50f;

    [DataField, AutoNetworkedField]
    public bool CrashOnZeroFuel = true;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public VehicleMoveState VehicleMove = VehicleMoveState.Disabled;

}

[Serializable, NetSerializable]
public enum VehicleMoveState : byte
{
    Disabled,
    Idle,
    Reduced,
    Active,
    Overdrive,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehiclePowerSourceStatesComponent : Component
{

    [DataField, AutoNetworkedField]
    public VehiclePowerSourceTankState PowerSourceTankState = VehiclePowerSourceTankState.Normal;

    [DataField, AutoNetworkedField]
    public VehiclePowerSourceEngineState PowerSourceEngineState = VehiclePowerSourceEngineState.Normal;
}

[Serializable, NetSerializable]
public enum VehiclePowerSourceTankState : byte
{
    Normal,
    Patched,
    Leaking,
    Torn,
    Ruptured,
    Gone,
}

[Serializable, NetSerializable]
public enum VehiclePowerSourceEngineState : byte
{
    Normal,
    Patched,
    Struggling,
    Gone,
}
