using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Applied to a humanoid after a mimic finishes the assimilation doafter on them.
/// Stores the identity snapshot mimics will use to impersonate this victim later.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationAssimilatedComponent : Component
{
    /// <summary>
    /// The mimic that performed the assimilation. Snapshots get fed into its mimic pool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AssimilatedBy;

    [DataField, AutoNetworkedField]
    public AbominationAssimilationProfile Profile = new();
}

/// <summary>
/// Snapshot of the data a mimic inherits when transforming into this assimilated form.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class AbominationAssimilationProfile
{
    [DataField]
    public string Name = string.Empty;

    /// <summary>
    /// NpcFactionMember factions transferred onto the mimic while disguised.
    /// </summary>
    [DataField]
    public List<string> Factions = new();

    /// <summary>
    /// UserIFF faction prototype IDs transferred onto the mimic while disguised.
    /// </summary>
    [DataField]
    public List<string> IffFactions = new();

    /// <summary>
    /// Source humanoid (NetEntity so the profile can travel inside networked state).
    /// Used at transform time to read SkillsComponent if it still exists.
    /// </summary>
    [DataField]
    public NetEntity? SourceEntity;

    [DataField]
    public AbominationAppearanceSnapshot? Appearance;

    /// <summary>
    /// Set for non-humanoid (animal) sources. The picker dedupes by this so
    /// every rat appears as a single "rat" entry, not one per assimilation.
    /// Null for humanoids — they're always unique entries by name.
    /// </summary>
    [DataField]
    public string? SourceProtoId;

    /// <summary>
    /// True if the source had TribalComponent. The mimic disguise re-adds it
    /// on the polymorphed entity so KillAllTribeRule (and anything else that
    /// scans for tribals) still counts the disguised mimic correctly, and
    /// other tribals won't aggro them.
    /// </summary>
    [DataField]
    public bool IsTribal;
}

/// <summary>
/// Snapshot of every HumanoidAppearanceComponent field the mimic disguise system
/// touches when applying / restoring a form. Built at assimilation time so the
/// disguise survives the source entity being deleted.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class AbominationAppearanceSnapshot
{
    [DataField]
    public ProtoId<SpeciesPrototype> Species;

    [DataField]
    public Color SkinColor = Color.FromHex("#C0967F");

    [DataField]
    public Color EyeColor = Color.Brown;

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public Gender Gender;

    [DataField]
    public int Age = 18;

    [DataField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();
}
