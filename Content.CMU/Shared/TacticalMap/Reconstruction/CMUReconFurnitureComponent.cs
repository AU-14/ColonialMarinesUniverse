namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>Opt-in furniture model for the initial tactical survey, inherited by colour variants.</summary>
[RegisterComponent]
public sealed partial class CMUReconFurnitureComponent : Component
{
    [DataField(required: true)]
    public CMUReconMaterial Material;
}
