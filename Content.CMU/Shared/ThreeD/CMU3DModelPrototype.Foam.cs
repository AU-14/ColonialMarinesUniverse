using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Static foam body and four independent source-owned SmoothEdge layers.</summary>
    [DataField]
    public CMU3DFoamAppearanceDefinition? FoamAppearance;
}

[DataDefinition]
public sealed partial class CMU3DFoamAppearanceDefinition
{
    [DataField(required: true)]
    public List<CMU3DModelPart> BaseParts = [];

    /// <summary>South/east/north/west lobes. Live visibility belongs to IconSmoothSystem.</summary>
    [DataField(required: true)]
    public Dictionary<string, CMU3DModelFrame> EdgeParts = [];
}
