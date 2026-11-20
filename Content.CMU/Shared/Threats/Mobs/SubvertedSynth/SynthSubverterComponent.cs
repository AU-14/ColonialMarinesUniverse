using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;

[RegisterComponent]
public sealed partial class SynthSubverterComponent : Component
{
    [DataField]
    public ComponentRegistry AdditionalComponents = new();

    [DataField]
    public LocId Briefing = "subverted-synth-briefing";

    [DataField]
    public string Role = "MindRoleFreeAgentSynth";

    [DataField]
    public SoundSpecifier? Sound;
}
