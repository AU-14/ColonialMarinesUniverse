using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>A named recovery story layered over a seeded wilderness layout.</summary>
[Prototype("cmuExpeditionScenario")]
public sealed partial class CMUExpeditionScenarioPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public EntProtoId Profile;
    [DataField(required: true)] public CMUExpeditionLandform Landform;
    [DataField(required: true)] public CMUExpeditionStory Story;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Briefing;
    [DataField(required: true)] public LocId History;
    [DataField(required: true)] public LocId TargetLore;
    [DataField(required: true)] public EntProtoId Orders;
    [DataField(required: true)] public EntProtoId FieldNote;
    [DataField(required: true)] public EntProtoId Recorder;
}
