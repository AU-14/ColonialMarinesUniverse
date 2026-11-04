namespace Content.Server.CMU14.ZLevels.Lighting;

/// <summary>
/// Additional floors of a game map share its ground map's sky lighting.
/// Roofs still mask ambient light independently on each floor.
/// </summary>
[RegisterComponent, UnsavedComponent, Access(typeof(CMUZLevelAmbientLightSystem))]
public sealed partial class CMUZLevelAmbientLightComponent : Component
{
    [DataField]
    public EntityUid Source;
}
