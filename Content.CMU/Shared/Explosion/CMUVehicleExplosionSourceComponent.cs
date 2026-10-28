namespace Content.Shared.CMU14.Explosion;

/// <summary>Retains the firing vehicle on an impact entity whose projectile is being deleted.</summary>
[RegisterComponent]
public sealed partial class CMUVehicleExplosionSourceComponent : Component
{
    [DataField]
    public EntityUid Vehicle;
}
