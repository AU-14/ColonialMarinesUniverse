using Content.Shared._RMC14.Vendors;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency;

/// <summary>
///     The single serializable shape that drives the whole INSFOR faction featureset.
///     Editors produce one of these, and the round-start apply pipeline consumes one.
///     Contains only data, never executable content, so it is safe to send over the wire
///     from a client-authored Custom faction file. The server always revalidates and
///     recomputes balance values from its own tables before trusting anything here.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class FactionDefinition
{
    // ---------------------------------------------------------------------
    // Tunables and caps. One place to change limits for the whole schema.
    // These are the guardrails the server enforces on untrusted (Custom) payloads.
    // Bump SchemaVersionCurrent whenever the shape changes so old saved factions
    // can still be loaded and upgraded with sensible defaults.
    // ---------------------------------------------------------------------
    public const int SchemaVersionCurrent = 1;

    // Payload sanity caps. The server clamps or rejects anything past these.
    public const int MaxTitleLength = 64;
    public const int MaxDescriptionLength = 2048;
    public const int MaxRoleplayTextLength = 4096;
    public const int MaxOpposedGovforFactions = 32;
    public const int MaxPlaceableEntities = 256;
    public const int MaxVendorDefinitions = 32;
    public const int MaxVendorSections = 64;
    public const int MaxVendorEntries = 512;
    public const int MaxRoleLoadouts = 64;
    public const int MaxRoleLoadoutContents = 128;
    public const int MaxVendorStock = 100_000;
    public const int MaxVendorPoints = 1_000_000;
    public const int MaxVendorSpawn = 100;
    public const int MaxSubmissionRatio = 1_000_000;

    // Default economy conversion if a definition does not set one.
    public const float DefaultDollarsToPointsRate = 1.0f;

    /// <summary>
    ///     Schema version this definition was written against. Load code fills in
    ///     defaults for anything a newer version added, so older saves never brick.
    /// </summary>
    [DataField]
    public int SchemaVersion = SchemaVersionCurrent;

    [DataField]
    public FactionMetadata Metadata = new();

    [DataField]
    public FactionEconomy Economy = new();

    [DataField]
    public CellKitManifest CellKit = new();

    /// <summary>
    ///     Per-role package contents, delivered after spawn via "A Package" (Phase 1b).
    /// </summary>
    [DataField]
    public List<FactionRoleLoadout> RoleLoadouts = new();
}

/// <summary>
///     Display and identity fields for a faction. Shown in antag info, spawn popup,
///     and recruit chat.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class FactionMetadata
{
    [DataField]
    public string Title = string.Empty;

    [DataField]
    public string Description = string.Empty;

    /// <summary>
    ///     The style of roleplay for this faction. Shown in antag info, recruit chat,
    ///     and the leader spawn popup so players know how they are meant to play.
    /// </summary>
    [DataField]
    public string RoleplayText = string.Empty;

    /// <summary>
    ///     Chat briefing shown to a member freshly recruited into this faction (for example via the
    ///     tattoo gun). Falls back to the default CLF recruit briefing when left empty.
    /// </summary>
    [DataField]
    public string RecruitedMessage = string.Empty;

    /// <summary>
    ///     Flag entity picked from the catalog. A wrenchable entity that can be added to
    ///     vendors and the cell kit. Phase 6 may point this at uploaded art instead.
    /// </summary>
    [DataField]
    public EntProtoId? FlagEntity;

    /// <summary>
    ///     Faction membership status icon, picked from the existing catalog. Used as the fallback for any
    ///     job that does not have its own entry in <see cref="JobStatusIcons"/>.
    /// </summary>
    [DataField]
    public ProtoId<FactionIconPrototype>? StatusIcon;

    /// <summary>
    ///     Status icon given to members recruited in-round (for example tattooed by the tattoo gun) who have
    ///     no per-job entry in <see cref="JobStatusIcons"/>. Without this such recruits keep the default CLF
    ///     icon instead of the faction's. Falls back to <see cref="StatusIcon"/> when left empty.
    /// </summary>
    [DataField]
    public ProtoId<FactionIconPrototype>? RecruitStatusIcon;

    /// <summary>
    ///     Per-job status icon overrides. A member whose job appears here shows that icon instead of the
    ///     faction-wide <see cref="StatusIcon"/>. Jobs with no entry fall back to <see cref="StatusIcon"/>.
    /// </summary>
    [DataField]
    public List<FactionJobIcon> JobStatusIcons = new();

    /// <summary>
    ///     Set on the DB copy that persistently overrides a code-built built-in faction (its
    ///     <see cref="InsurgencyBuiltinFactions.VanillaClfId"/>-style id). The editor upserts this one row
    ///     instead of spawning a fresh faction every time the built-in is edited and saved, so the built-in
    ///     becomes editable like any authored faction. Null on normal factions.
    /// </summary>
    [DataField]
    public int? BuiltinOverrideOf;

    /// <summary>
    ///     Which GOVFOR factions this Default faction is allowed to oppose, by platoon id
    ///     (USMC, TWE RMC, UPP, and so on). Matched against the round's selected GOVFOR platoon.
    ///     Only meaningful for Default factions. Custom factions ignore this and are
    ///     gated by the whitelist instead.
    /// </summary>
    [DataField]
    public List<string> OpposedGovforFactions = new();
}

/// <summary>
///     One per-job status-icon override: the job whose members should show <see cref="Icon"/> instead of
///     the faction-wide status icon.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class FactionJobIcon
{
    /// <summary>Job prototype id this override applies to (for example "AU14JobCLFCellLeader").</summary>
    [DataField]
    public string Role = string.Empty;

    /// <summary>Status icon shown for members of <see cref="Role"/>.</summary>
    [DataField]
    public ProtoId<FactionIconPrototype>? Icon;
}

/// <summary>
///     Economy tuning for a faction.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class FactionEconomy
{
    /// <summary>
    ///     Conversion from intel dollars to vendor points. The server owns the real
    ///     rate; a Custom payload value is clamped before use.
    /// </summary>
    [DataField]
    public float DollarsToPointsRate = FactionDefinition.DefaultDollarsToPointsRate;

    /// <summary>
    ///     What the analyzer machine accepts and converts into cell points, on top of (or instead of)
    ///     plain dollars. The editor exposes these so the creator can add, change, or remove what can be
    ///     submitted and at what ratio.
    /// </summary>
    [DataField]
    public List<PointsSubmissionEntry> PointsSubmissions = new();

    /// <summary>
    ///     Whether plain dollars still convert to points at the analyzer's built-in rate even when custom
    ///     submittables are configured. On by default so adding a custom item never silently disables
    ///     cash. Turn off for a faction whose economy should reject dollars entirely.
    /// </summary>
    [DataField]
    public bool IncludeDollars = true;
}

/// <summary>
///     One submittable-for-points item. Two ratio styles are supported and the creator picks per entry:
///     "<see cref="AmountPerPoint"/> of the entity make one point" (good for cheap goods), or "one entity
///     is worth <see cref="PointsPerItem"/> points" (good for valuable goods). <see cref="PointsPerItemMode"/>
///     selects which one is used.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class PointsSubmissionEntry
{
    /// <summary>
    ///     The entity that can be fed into the analyzer. Stackable entities count by their stack size.
    /// </summary>
    [DataField]
    public EntProtoId Entity;

    /// <summary>
    ///     When true, one of <see cref="Entity"/> is worth <see cref="PointsPerItem"/> points. When false,
    ///     it takes <see cref="AmountPerPoint"/> of the entity to earn a single point.
    /// </summary>
    [DataField]
    public bool PointsPerItemMode;

    /// <summary>
    ///     How many of <see cref="Entity"/> convert into a single point, in amount-per-point mode. Clamped
    ///     to at least one so a submission can never mint infinite points.
    /// </summary>
    [DataField]
    public int AmountPerPoint = 15;

    /// <summary>
    ///     How many points a single <see cref="Entity"/> is worth, in points-per-item mode. Clamped to at
    ///     least one.
    /// </summary>
    [DataField]
    public int PointsPerItem = 1;
}

/// <summary>
///     What the Heavy Cell Kit / "A Package" can deploy for this faction.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class CellKitManifest
{
    /// <summary>
    ///     Individual entities the leader can free-place from the cell kit.
    /// </summary>
    [DataField]
    public List<EntProtoId> PlaceableEntities = new();

    /// <summary>
    ///     Vendors the faction can deploy. Each carries a base model plus its sections.
    /// </summary>
    [DataField]
    public List<FactionVendorDefinition> VendorDefinitions = new();
}

/// <summary>
///     A deployable vendor: an existing vendor entity used as the sprite and collision
///     base, plus the sections that get injected into it at apply time. Reuses the
///     existing <see cref="CMVendorSection"/> type so editing an arsenal is just editing
///     these sections.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class FactionVendorDefinition
{
    /// <summary>
    ///     Display name shown on the deployed vendor.
    /// </summary>
    [DataField]
    public string Name = string.Empty;

    /// <summary>
    ///     Existing vendor entity used as the sprite and collision base. The faction's
    ///     sections are injected into a spawned copy of this at apply time.
    /// </summary>
    [DataField]
    public EntProtoId BaseModel;

    /// <summary>
    ///     Arsenal contents. Reuses the vendor section type, which already carries
    ///     point cost, stock amount, and max per section.
    /// </summary>
    [DataField]
    public List<CMVendorSection> Sections = new();

    /// <summary>
    ///     When true the deployed vendor is built unanchored and can be freely wrenched down and moved,
    ///     whatever the base entity normally allows. When false it is not wrenchable at all, so it cannot
    ///     be picked up or repositioned once placed.
    /// </summary>
    [DataField]
    public bool Wrenchable = true;

    /// <summary>
    ///     When true the deployed vendor is made invulnerable, so a base entity that would normally
    ///     break, change state, or turn into another entity on taking damage stays intact.
    /// </summary>
    [DataField]
    public bool Invulnerable;

    /// <summary>
    ///     When true the vendor spends the cell's shared intel points (fed by the intel computer)
    ///     instead of the buyer's individual vendor points, so submitting money at the intel machine
    ///     stocks the whole cell's buying power.
    /// </summary>
    [DataField]
    public bool UsesIntelPoints = true;

    /// <summary>
    ///     When true the base model's own vendor configuration is kept as-is: its prototype sections,
    ///     points mode, and access are left untouched instead of being replaced by this definition.
    ///     Used by built-in factions (the vanilla CLF faction) that reuse a real, fully-configured
    ///     GOVFOR/CLF vendor prototype without re-authoring its arsenal. Custom factions never set this.
    /// </summary>
    [DataField]
    public bool UseBaseModelSections;
}

/// <summary>
///     Contents of one role's "A Package". Delivered in-hand after the faction is chosen.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class FactionRoleLoadout
{
    /// <summary>
    ///     Job this loadout is for, by job prototype id string (for example
    ///     "AU14JobCLFCellLeader").
    /// </summary>
    [DataField]
    public string Role = string.Empty;

    /// <summary>
    ///     Entities spawned when this role uses their package.
    /// </summary>
    [DataField]
    public List<EntProtoId> Contents = new();
}
