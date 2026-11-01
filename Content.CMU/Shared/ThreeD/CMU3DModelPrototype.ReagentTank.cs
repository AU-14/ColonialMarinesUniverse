using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Opt-in for the proven four-layer reagent vessel. Its Fill layer remains source-owned.</summary>
    [DataField]
    public CMU3DReagentTankAppearanceDefinition? ReagentTankAppearance;
}

[DataDefinition]
public sealed partial class CMU3DReagentTankAppearanceDefinition
{
    /// <summary>Unique white-multiplier parts carrying the identical opaque permanent/fill source mask.</summary>
    [DataField(required: true)]
    public string[] VesselParts = [];
}
