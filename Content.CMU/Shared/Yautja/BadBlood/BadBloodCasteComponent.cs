using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// On a Bad Blood who has taken up a caste (ARCH-04 to ARCH-06; CMU14 ARCH-604: castes are prototypes). The caste's
/// powers are the marker components its prototype adds, not this.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodCasteComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<BadBloodCastePrototype> Caste;

    /// <summary>
    /// CMU14 ARCH-701: each skill the caste raised, at the level it had before. Dropping the caste puts them back.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> SkillsBefore = new();

    /// <summary>
    /// CMU14 ARCH-701: the components the caste added that the body did not already have. Dropping the caste removes
    /// exactly these.
    /// </summary>
    [DataField]
    public HashSet<string> AddedComponents = new();
}

/// <summary>
/// CMU14 ARCH-604: the Lodge's "Choose your caste" verb sits on the entity that carries this (BadBloodCasteSystem).
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCasteChoiceComponent : Component;

/// <summary>
/// CMU14 ARCH-604: the Lodge's caste dialog sends this back when a Bad Blood picks <see cref="Caste"/>.
/// <see cref="Lodge"/> is the Lodge the dialog was opened at: the choice only counts while the user still stands there.
/// </summary>
[Serializable, NetSerializable]
public sealed record BadBloodCasteChosenEvent(NetEntity User, ProtoId<BadBloodCastePrototype> Caste, NetEntity Lodge);

/// <summary>
/// CMU14 ARCH-604 (Engineer): builds anywhere, the Lodge's build range aside (BadBloodQuarrySystem).
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodBuildAnywhereComponent : Component;

/// <summary>
/// CMU14 ARCH-604 (Engineer): the mask visor sees as an honourable hunter's does (YautjaMaskSystem).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodHunterVisorComponent : Component;

/// <summary>
/// CMU14 ARCH-604 (Medic): Yautja medical and surgical steps take <see cref="Multiplier"/> of their time
/// (CMUSurgeryFlowSystem).
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodMedicSpeedComponent : Component
{
    [DataField]
    public float Multiplier = 0.6f;
}
