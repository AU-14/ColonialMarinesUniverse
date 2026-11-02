namespace Content.Server.CMU14.Atmos;

/// <summary>Initial atmosphere for hatch volumes isolated by gunship floor fans.</summary>
[RegisterComponent]
public sealed partial class CMUGunshipDoorAtmosphereComponent : Component
{
    /// <summary>Saved so loading an existing ship does not refill a depressurized hatch.</summary>
    [DataField]
    public bool Initialized;

    public bool MapInitialized;
}
