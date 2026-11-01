namespace Content.Server.CMU14.Light;

/// <summary>
///     Opt-out from the warship alert light tint. Maint-style fixtures keep their
///     own lamp color through every alert level.
/// </summary>
[RegisterComponent]
public sealed partial class CMUWarshipAlertLightExemptComponent : Component;
