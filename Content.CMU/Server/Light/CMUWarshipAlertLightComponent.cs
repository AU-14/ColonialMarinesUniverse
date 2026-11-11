using Content.Shared._RMC14.AlertLevel;

namespace Content.Server.CMU14.Light;

/// <summary>
///     Runtime marker for ship light fixtures recolored by warship alert level.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUWarshipAlertLightsSystem))]
public sealed partial class CMUWarshipAlertLightComponent : Component
{
    public Color? Original;

    /// <summary>
    ///     Rotating beacon child spawned while the ship sits on an armed alert level.
    /// </summary>
    public EntityUid? Beacon;

    /// <summary>
    ///     Level the current beacon was spawned for, a mismatch means respawn.
    /// </summary>
    public RMCAlertLevels? BeaconLevel;
}
