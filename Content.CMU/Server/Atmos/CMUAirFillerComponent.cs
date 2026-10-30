using Content.Shared.Atmos;

namespace Content.Server.CMU14.Atmos;

/// <summary>
///     A self-contained vent that tops its room up with breathable air: 21% oxygen, 79% nitrogen at room temperature.
///     It stops once the room reaches <see cref="TargetPressure"/> and starts again whenever the pressure drops.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAirFillerComponent : Component
{
    /// <summary>The pressure, in kPa, it fills the room up to.</summary>
    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    /// <summary>The most air, in moles, it puts out each second.</summary>
    [DataField]
    public float MaxMolesPerSecond = 50f;

    [DataField]
    public float Temperature = Atmospherics.T20C;

    [DataField]
    public float OxygenFraction = 0.21f;

    /// <summary>Whether it was pushing air out at the last check, for the sprite.</summary>
    [ViewVariables]
    public bool Filling;
}
