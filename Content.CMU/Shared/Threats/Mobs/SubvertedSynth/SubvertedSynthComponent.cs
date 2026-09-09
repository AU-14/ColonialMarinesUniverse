using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
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

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    // Runtime provenance of this key's overlay. Snapshots contain only the
    // component's serialized data, and applied handles identify exactly which
    // live instances reset may retire. These are not network payloads.
    public Dictionary<Type, (Component Applied, Component? Previous)> ComponentOverlays = new();

    // Non-null only when this key introduced the faction/member, rather than
    // encountering an independently existing membership or faction icon.
    public NpcFactionMemberComponent? AddedFactionTo;
    public CLFMemberComponent? AddedClfMember;

    public override bool SessionSpecific => true;
}
