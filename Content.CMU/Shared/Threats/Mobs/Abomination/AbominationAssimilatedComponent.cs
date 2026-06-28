using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Applied to a humanoid after a mimic finishes the assimilation doafter on them.
///     Stores the identity snapshot mimics will use to impersonate this victim later.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationAssimilatedComponent : Component
{
    /// <summary>
    ///     The mimic that performed the assimilation. Snapshots get fed into its mimic pool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AssimilatedBy;

    [DataField, AutoNetworkedField]
    public AbominationAssimilationProfile Profile = new();
}

/// <summary>
///     Snapshot of the data a mimic inherits when transforming into this assimilated form.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class AbominationAssimilationProfile
{
    [DataField]
    public AbominationAppearanceSnapshot? Appearance;

    /// <summary>
    ///     NpcFactionMember factions transferred onto the mimic while disguised.
    /// </summary>
    [DataField]
    public List<string> Factions = new();

    /// <summary>
    ///     UserIFF faction prototype IDs transferred onto the mimic while disguised.
    /// </summary>
    [DataField]
    public List<string> IffFactions = new();

    /// <summary>
    ///     True if the source had TribalComponent. The mimic disguise re-adds it
    ///     on the polymorphed entity so KillAllTribeRule (and anything else that
    ///     scans for tribals) still counts the disguised mimic correctly, and
    ///     other tribals won't aggro them.
    /// </summary>
    [DataField]
    public bool IsTribal;

    [DataField]
    public string Name = string.Empty;

    /// <summary>
    ///     Source humanoid (NetEntity so the profile can travel inside networked state).
    ///     Used at transform time to read SkillsComponent if it still exists.
    /// </summary>
    [DataField]
    public NetEntity? SourceEntity;

    /// <summary>
    ///     Set for non-humanoid (animal) sources. The picker dedupes by this so
    ///     every rat appears as a single "rat" entry, not one per assimilation.
    ///     Null for humanoids — they're always unique entries by name.
    /// </summary>
    [DataField]
    public string? SourceProtoId;

    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();
}

/// <summary>
///     Snapshot of every HumanoidAppearanceComponent field the mimic disguise system
///     touches when applying / restoring a form. Built at assimilation time so the
///     disguise survives the source entity being deleted.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class AbominationAppearanceSnapshot
{
    [DataField]
    public int Age = 18;

    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField]
    public Color EyeColor = Color.Brown;

    [DataField]
    public Gender Gender;

    [DataField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public Color SkinColor = Color.FromHex("#C0967F");

    [DataField]
    public ProtoId<SpeciesPrototype> Species;
}
