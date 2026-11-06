namespace Content.Client.CMU14.Dropship.AttachmentPoint;

/// <summary>Owns the client-side lamp that follows a door gun's visual aim.</summary>
[RegisterComponent]
public sealed partial class CMUDoorGunLightComponent : Component
{
    public EntityUid? Light;
}
