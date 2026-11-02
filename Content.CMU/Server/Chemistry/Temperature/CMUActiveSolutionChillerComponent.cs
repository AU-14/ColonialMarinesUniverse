namespace Content.Server.CMU14.Chemistry.Temperature;

/// <summary>
/// Present while a <see cref="CMUSolutionChillerComponent"/> is powered and has something placed on it.
/// </summary>
[RegisterComponent, Access(typeof(CMUSolutionChillerSystem))]
public sealed partial class CMUActiveSolutionChillerComponent : Component;
