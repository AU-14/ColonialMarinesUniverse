using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.SubvertedSynth;

[RegisterComponent, NetworkedComponent]
public sealed partial class SubvertedSynthComponent : Component
{
    [DataField]
    public ComponentRegistry AdditionalComponents = new();

    [DataField]
    public SoundSpecifier CLFSubversionSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    public override bool SessionSpecific => true;
}
