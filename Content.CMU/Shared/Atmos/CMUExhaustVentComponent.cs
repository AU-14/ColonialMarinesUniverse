namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// One-directional waste dump. Deletes inlet line gas into open sky: on planet
/// maps the relaxation reservoir eats it, on anything else it behaves like a
/// line vented to space. Requires a sky-exposed tile, which the system checks
/// against the same rule the relaxation pass uses.
/// </summary>
[RegisterComponent]
public sealed partial class CMUExhaustVentComponent : Component
{
    /// <summary>kPa floor on the inlet line. Below it the stack idles.</summary>
    [DataField]
    public float MinInletPressure = 20f;

    /// <summary>Moles dumped per atmos tick.</summary>
    [DataField]
    public float MaxTransferMoles = 25f;

    [DataField]
    public string InletName = "pipe";

    /// <summary>True when the stack can discharge. False when manually shut.</summary>
    [ViewVariables]
    public bool IsOpen = true;
}
