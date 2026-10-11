namespace Content.Server.CMU14.Light;

/// <summary>
/// Added to a helmet once a helmet-mountable light is stored in it,
/// so equipping or removing the helmet updates the stored lights.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHelmetLightHolderComponent : Component;
