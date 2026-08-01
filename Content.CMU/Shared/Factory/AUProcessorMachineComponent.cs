using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Shared.AU14.Factory;

[RegisterComponent]
public sealed partial class AUProcessorMachineComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> InputTag;

    [DataField(required: true)]
    public EntProtoId Output;

    [DataField]
    public float ProcessTime = 5f;

    [DataField]
    public SoundSpecifier ProcessSound = new SoundPathSpecifier("/Audio/Machines/reclaimer_startup.ogg");

    [DataField]
    public string InputSlotId = "processorInput";

    [DataField]
    public float BreakChance = 0.1f;

    [DataField]
    public EntProtoId? FireSpawn = "RMCTileFire";

    public bool IsBroken;

    // Transient runtime state
    public EntityUid? PendingItem;
    public float TimeRemaining;
    public bool IsProcessing;
}
