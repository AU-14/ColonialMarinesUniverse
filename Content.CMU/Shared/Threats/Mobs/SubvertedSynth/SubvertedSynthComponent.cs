using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;

[RegisterComponent, NetworkedComponent]
public sealed partial class SubvertedSynthComponent : Component
{
    [DataField]
    public ComponentRegistry AdditionalComponents = new();

    [DataField]
    public SoundSpecifier CLFSubversionSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    // Runtime provenance of this key's overlay. Snapshots contain only the
    // component's serialized data, and applied handles identify exactly which
    // live instances reset may retire. These are not network payloads.
    public Dictionary<Type, (Component Applied, Component? Previous)> ComponentOverlays = new();
    public List<Component> RemovedComponents = new();

    public override bool SessionSpecific => true;
}
