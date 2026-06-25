using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.SubvertedSynth;

[RegisterComponent]
public sealed partial class SynthSubverterComponent : Component
{
    [DataField]
    public ComponentRegistry AdditionalComponents = new();

    [DataField]
    public LocId Briefing = "clf-subverted-synth-briefing";

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    [DataField]
    public string Role = "MindRoleCLFSubvertedSynth";

    [DataField]
    public SoundSpecifier? Sound;
}
