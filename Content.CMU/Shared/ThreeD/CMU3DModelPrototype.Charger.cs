using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Bounded charging-dock layers sampled from their original visual owners.</summary>
    [DataField]
    public CMU3DChargerAppearanceDefinition? ChargerAppearance;
}

[DataDefinition]
public sealed partial class CMU3DChargerAppearanceDefinition
{
    [DataField(required: true)]
    public List<CMU3DModelPart> BaseParts = [];

    /// <summary>Six original indicator states. Delays are portable export metadata, never a live clock.</summary>
    [DataField(required: true)]
    public Dictionary<string, CMU3DSpriteState> LightStates = [];

    /// <summary>Original ItemMapper taser/baton overlays, independent of charge level.</summary>
    [DataField(required: true)]
    public Dictionary<string, CMU3DModelFrame> InsertedStates = [];
}
