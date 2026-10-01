namespace Content.Server.CMU14.Light;

/// <summary>
///     Exterior warship floodlight that goes dark on red alert, submarine style.
///     Interior fixtures tint their color instead; these switch off entirely.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUWarshipAlertLightsSystem))]
public sealed partial class CMUWarshipExteriorLightComponent : Component
{
    /// <summary>
    ///     Enabled state before the alert cull, restored once the alert drops below red.
    /// </summary>
    public bool? OriginalEnabled;

    /// <summary>
    ///     Color before the alert cull, halved on delta instead of going dark.
    /// </summary>
    public Color? Original;
}
