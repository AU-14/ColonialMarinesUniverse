namespace Content.Server.CMU14.Chemistry.Temperature;

/// <summary>
/// Present while a <see cref="Content.Shared.CMU14.Chemistry.Temperature.CMUThermostatPlateComponent"/> is switched on,
/// powered and has something placed on it.
/// </summary>
[RegisterComponent, Access(typeof(CMUThermostatPlateSystem))]
public sealed partial class CMUActiveThermostatPlateComponent : Component;
