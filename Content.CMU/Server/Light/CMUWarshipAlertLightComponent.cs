namespace Content.Server.CMU14.Light;

/// <summary>
///     Runtime marker for ship light fixtures recolored by warship alert level.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUWarshipAlertLightsSystem))]
public sealed partial class CMUWarshipAlertLightComponent : Component
{
    public Color? Original;
}
