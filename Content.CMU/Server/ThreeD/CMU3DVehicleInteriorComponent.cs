namespace Content.Server.CMU14.ThreeD;

/// <summary>The private interior whose 3D availability follows this vehicle's map.</summary>
[RegisterComponent]
public sealed partial class CMU3DVehicleInteriorComponent : Component
{
    public EntityUid Map;
}
