using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SynthGenerationComponent : Component
{
    /// <summary>
    /// Localization ID used to describe this generation as a humanoid age.
    /// </summary>
    [DataField(required: true)]
    public LocId AgeRepresentation;

    /// <summary>
    /// I.E. 1st generation, 3rd generation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Generation;

    [DataField, AutoNetworkedField]
    public EntProtoId GenerationAction = "ActionChooseGen";

    [DataField, AutoNetworkedField]
    public EntityUid? SelectGenerationActionEntity;

    [DataField]
    public ProtoId<DamageModifierSetPrototype>? DamageModifier;

    [DataField]
    public int Priority;
}
