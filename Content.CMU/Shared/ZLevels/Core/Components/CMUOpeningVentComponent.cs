namespace Content.Shared.CMU14.ZLevels.Core.Components;

[RegisterComponent]
public sealed partial class CMUOpeningVentComponent : Component
{
    /// <summary>kPa difference below which the vent idles.</summary>
    [DataField]
    public float Threshold = 5f;

    /// <summary>Moles moved per atmos tick, per half. A pair runs both halves, so the pair moves up to twice this.</summary>
    [DataField]
    public float MaxTransferMoles = 2f;

    /// <summary>
    /// kPa floor on either deck. Below it the pair auto-seals so a holed deck
    /// cannot drain the whole z-network. Upstream vent pump lockout parity.
    /// </summary>
    [DataField]
    public float LockoutThreshold = 80f;

    /// <summary>True when gas transfer is active. False when manually shut.</summary>
    [ViewVariables]
    public bool IsOpen = true;
}
