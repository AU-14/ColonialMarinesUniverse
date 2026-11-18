using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// CMU14 Playtest 5 (docs/GraphAudit-BadBlood-Playtest5Plan.md): the caste signature actives, D4.

/// <summary>
/// CMU14 ECS-703: a Berserker in a Blood Rage. Melee hits harder (BadBloodMeleeSystem), slows do not take, the cloak
/// stays off, and the Berserker counts double toward seizing a Hunt Ground. Networked so the client tints the sprite
/// red while it lasts (BadBloodBloodRageVisualizerSystem). Removed when it ends, the light and tint with it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodBloodRageComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public Color Tint = Color.FromHex("#ff7070");

    /// <summary>
    /// The rage added the body's point light, so the end takes it away again.
    /// </summary>
    [DataField]
    public bool AddedLight;
}

public sealed partial class BadBloodBloodRageActionEvent : InstantActionEvent;

/// <summary>
/// CMU14 ECS-705: the Hunt Leader's War Cry action. Follows leadership (BadBloodLeaderPerksSystem).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodWarCryComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "CMUActionBadBloodWarCry";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public float Radius = 7f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);

    [DataField]
    public float SpeedMultiplier = 1.15f;

    /// <summary>
    /// Slows and stuns on a clanmate who heard the cry last this share of their time.
    /// </summary>
    [DataField]
    public float StaggerMultiplier = 0.5f;

    [DataField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("CMUYautjaRoars");
}

public sealed partial class BadBloodWarCryActionEvent : InstantActionEvent;

/// <summary>
/// CMU14 ECS-705: a clanmate who heard the Hunt Leader's War Cry: faster, and staggers for less, until
/// <see cref="EndsAt"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodWarCriedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.15f;

    [DataField, AutoNetworkedField]
    public float StaggerMultiplier = 0.5f;
}

/// <summary>
/// CMU14 FLOW-701 (D3): the Hunt Leader's command sight. The Leader no longer wears the honourable hunters' visor;
/// instead the client draws clanmates, clan beasts and Hunt Grounds within <see cref="Range"/> through walls
/// (BadBloodCommandSightOverlay). Follows leadership (BadBloodLeaderPerksSystem).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodCommandSightComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 20f;
}

/// <summary>
/// CMU14 ECS-701: the Engineer caste's signature active and trade. Raise Bulwark builds a line of clan barricades from
/// bricks in hand; reconsecration takes <see cref="ConsecrationTimeMultiplier"/> of its time; the Engineer counts
/// <see cref="ContestWeight"/> toward seizing a Hunt Ground; and a Repair verb mends a Totem or the Lodge.
/// BadBloodEngineerSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodEngineerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId BulwarkAction = "CMUActionBadBloodRaiseBulwark";

    [DataField, AutoNetworkedField]
    public EntityUid? BulwarkActionEntity;

    [DataField]
    public EntProtoId BarricadePrototype = "CMUBadBloodBarricade";

    /// <summary>
    /// Bricks each barricade of the bulwark costs.
    /// </summary>
    [DataField]
    public int BricksPerBarricade = 2;

    /// <summary>
    /// How many tiles wide the bulwark is, centred in front of the Engineer.
    /// </summary>
    [DataField]
    public int BulwarkWidth = 3;

    [DataField]
    public float ConsecrationTimeMultiplier = 0.5f;

    [DataField]
    public float ContestWeight = 1.5f;

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Damage a repair heals from a Totem or the Lodge.
    /// </summary>
    [DataField]
    public float RepairAmount = 100f;
}

public sealed partial class BadBloodRaiseBulwarkActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class BadBloodRepairDoAfterEvent : SimpleDoAfterEvent;
