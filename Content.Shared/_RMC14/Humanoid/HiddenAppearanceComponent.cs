using Content.Shared.Whitelist;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Humanoid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class HiddenAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public HiddenHumanoidAppearance? Appearance;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;
}

/// <summary>
/// A presentation-only humanoid appearance used to conceal an entity's authoritative identity.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class HiddenHumanoidAppearance
{
    [DataField(required: true)]
    public ProtoId<SpeciesPrototype> Species;

    [DataField]
    public Sex Sex;

    [DataField(required: true)]
    public HumanoidCharacterAppearance Appearance = default!;

    public HiddenHumanoidAppearance(
        ProtoId<SpeciesPrototype> species,
        Sex sex,
        HumanoidCharacterAppearance appearance)
    {
        Species = species;
        Sex = sex;
        Appearance = appearance.Clone();
    }
}
