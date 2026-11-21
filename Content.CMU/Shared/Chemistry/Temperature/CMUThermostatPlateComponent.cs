using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Chemistry.Temperature;

/// <summary>
/// A plate (hotplate, chiller) that brings every solution placed on its <c>ItemPlacer</c> towards
/// <see cref="TargetTemperature"/>, but only while it is switched on, powered and has something on it.
/// It never overshoots the target and only works in the direction(s) its <see cref="Mode"/> allows.
/// Run by the server <c>CMUThermostatPlateSystem</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUThermostatPlateComponent : Component
{
    /// <summary>
    /// Whether a player switched the plate on. Plates start off when built.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    /// The temperature, in kelvin, the plate brings placed solutions to. Kept inside [<see cref="MinTarget"/>, <see cref="MaxTarget"/>].
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TargetTemperature = 293.15f;

    [DataField]
    public float MinTarget = 293.15f;

    [DataField]
    public float MaxTarget = 600f;

    /// <summary>
    /// Thermal energy moved into or out of each placed solution per second.
    /// </summary>
    [DataField]
    public float EnergyPerSecond = 160f;

    [DataField]
    public CMUThermostatMode Mode = CMUThermostatMode.HeatOnly;
}

/// <summary>
/// Which way a thermostat may move a solution's temperature.
/// </summary>
[Serializable, NetSerializable]
public enum CMUThermostatMode : byte
{
    HeatOnly,
    CoolOnly,
    Both,
}
