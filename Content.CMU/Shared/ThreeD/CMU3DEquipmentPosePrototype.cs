using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>
/// Presentation attachment for an explicitly modeled item. Body poses face -Y;
/// held poses use the existing replicated inventory and hand owners.
/// </summary>
[Prototype("cmu3DEquipmentPose")]
public sealed partial class CMU3DEquipmentPosePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;
    [DataField(required: true)] public string[] SourcePrototypes = [];
    [DataField(required: true)] public ProtoId<CMU3DModelPrototype> Model;
    [DataField(required: true)] public string Slot = "hand";
    [DataField] public Vector3 Pivot;
    [DataField] public Vector3 Offset;
    [DataField] public float Scale = 1;
    [DataField] public float Yaw;
    [DataField] public float Pitch;
    [DataField] public float Roll;
    [DataField] public bool WornAppearance;
    /// <summary>Ordered, fully composed source layers. An unmodeled live overlay keeps its sprite fallback.</summary>
    [DataField] public List<CMU3DEquipmentLayer> WornLayers = [];
    /// <summary>Exact ground appearance represented by a held static model, including attachment overlays.</summary>
    [DataField] public List<CMU3DEquipmentLayer> ItemLayers = [];
    [DataField] public Vector3 FirstPersonOffset = new(.25f, .55f, -.35f);
}

[DataDefinition]
public sealed partial class CMU3DEquipmentLayer
{
    [DataField(required: true)] public string Rsi = string.Empty;
    [DataField(required: true)] public string State = string.Empty;
    [DataField] public Color Color = Color.White;
    [DataField] public Vector2 Offset;
}
