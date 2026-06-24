using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.NPC.Prototypes;

namespace Content.Shared._CMU14.CLFSubverter;

[RegisterComponent, NetworkedComponent]
public sealed partial class LFSubvertedSynthComponent : Component
{
    [DataField]
    public SoundSpecifier CLFSubversionSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    [DataField]
    public ComponentRegistry AdditionalComponents = new();

    public override bool SessionSpecific => true;
}