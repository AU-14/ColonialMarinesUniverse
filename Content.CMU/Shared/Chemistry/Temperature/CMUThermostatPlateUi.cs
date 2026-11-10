using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Chemistry.Temperature;

// Key of the thermostat plate window. The window reads the networked CMUThermostatPlateComponent, so there is no BUI state.
[Serializable, NetSerializable]
public enum CMUThermostatPlateUiKey : byte
{
    Key,
}

// From the window: set the plate's target temperature in kelvin (the server clamps it to the plate's range).
[Serializable, NetSerializable]
public sealed class CMUThermostatSetTargetMessage(float target) : BoundUserInterfaceMessage
{
    public float Target = target;
}

// From the window: switch the plate on or off.
[Serializable, NetSerializable]
public sealed class CMUThermostatToggleMessage : BoundUserInterfaceMessage
{
}
