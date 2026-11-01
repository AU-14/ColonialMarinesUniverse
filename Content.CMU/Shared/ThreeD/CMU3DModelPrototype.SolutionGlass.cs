using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Source-keyed vessel/fill layers, including explicitly authored metamorphic vessels.</summary>
    [DataField]
    public CMU3DSolutionAppearanceDefinition? SolutionAppearance;
}

[DataDefinition]
public sealed partial class CMU3DSolutionAppearanceDefinition
{
    [DataField(required: true)] public List<CMU3DSolutionLayerGeometry> Layers = [];
    [DataField(required: true)] public List<CMU3DSolutionLayerPose> DefaultLayers = [];
}

[DataDefinition]
public sealed partial class CMU3DSolutionLayerGeometry
{
    [DataField(required: true)] public string Role = string.Empty;
    [DataField(required: true)] public string Rsi = string.Empty;
    [DataField(required: true)] public string State = string.Empty;
    [DataField(required: true)] public List<CMU3DModelPart> Parts = [];
}

[DataDefinition]
public sealed partial class CMU3DSolutionLayerPose
{
    [DataField(required: true)] public string Role = string.Empty;
    [DataField(required: true)] public string Rsi = string.Empty;
    [DataField(required: true)] public string State = string.Empty;
    [DataField] public Color Color = Color.White;
    [DataField] public bool Visible = true;
}
