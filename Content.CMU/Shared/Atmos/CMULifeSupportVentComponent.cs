using Content.Shared.Atmos;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Mapper-only floor vent. Acts as fill and scrubber in one: it restores the
/// tile under it to a fixed oxygen/nitrogen mix at target pressure, topping
/// up shortfalls and pulling excess or waste gases back out, forever, with no
/// pipe or power hookup. Keeps shuttle interiors breathable after a fire.
/// </summary>
[RegisterComponent]
public sealed partial class CMULifeSupportVentComponent : Component
{
    /// <summary>kPa ceiling. At or above it the vent idles.</summary>
    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    /// <summary>Oxygen share of the mix by moles; the remainder is nitrogen.</summary>
    [DataField]
    public float OxygenFraction = Atmospherics.OxygenStandard;

    /// <summary>Moles per second the vent moves each gas toward the target mix, in or out.</summary>
    [DataField]
    public float TransferRate = Atmospherics.MolesCellStandard * 20f;
}
