using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marks a Working Joe as the Early Warning Joe antagonist and tracks its level progression.
/// Levels 0-5 are defined by <see cref="EWJoeLevelsPrototype"/>; each level grants actions and
/// components that are tracked here so they can be removed cleanly on level change or cleanup.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeComponent : Component
{
    /// <summary>The antag's identity must not leak to other clients; only the Joe predicts its own abilities.</summary>
    public override bool SendOnlyToOwner => true;

    /// <summary>Current level, 0 (baseline) to 5 (Melding).</summary>
    [DataField, AutoNetworkedField]
    public int Level;

    /// <summary>Highest level whose grants have been applied; -1 before the first grant. Levels never go down.</summary>
    [DataField]
    public int AppliedLevel = -1;

    /// <summary>The mob's own innate slowdown, recorded on the first grant and restored on cleanup.</summary>
    [DataField]
    public float? OriginalSlowdown;

    /// <summary>The level table that defines per-level grants.</summary>
    [DataField]
    public ProtoId<EWJoeLevelsPrototype> LevelTable = "CMUEWJoeLevels";

    /// <summary>Melee and bullet armor added per level (Sculpted Form).</summary>
    [DataField, AutoNetworkedField]
    public int ArmorPerLevel = 5;

    /// <summary>The innate slowdown the Joe starts with; shed by <see cref="SlowdownShedPerLevel"/> each level.</summary>
    [DataField, AutoNetworkedField]
    public float BaseSlowdown = 1.25f;

    [DataField, AutoNetworkedField]
    public float SlowdownShedPerLevel = 0.25f;

    /// <summary>Test of Faith: both the Joe and the target must face each other within this range.</summary>
    [DataField, AutoNetworkedField]
    public float TestOfFaithRange = 6f;

    [DataField, AutoNetworkedField]
    public TimeSpan TestOfFaithFlash = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan TestOfFaithStun = TimeSpan.FromSeconds(1.5);

    /// <summary>Disrupt: the level-0 EMP pulse. Lights, headsets and batteries respond today.</summary>
    [DataField, AutoNetworkedField]
    public float DisruptRange = 3f;

    [DataField, AutoNetworkedField]
    public float DisruptEnergy = 50000f;

    [DataField, AutoNetworkedField]
    public TimeSpan DisruptDuration = TimeSpan.FromSeconds(10);

    /// <summary>Inventory slot the overwatch camera is removed from at selection (D13).</summary>
    [DataField]
    public string CameraSlot = "ears2";

    /// <summary>Actions granted by the level table, in grant order. Server bookkeeping.</summary>
    [DataField]
    public List<EntityUid> GrantedActions = new();

    /// <summary>Component names granted by the level table, keyed to the level that granted them.</summary>
    [DataField]
    public Dictionary<string, int> GrantedComponents = new();

    [DataField]
    public List<EntityUid> Thralls = new();

    [DataField]
    public int ConsolesAssimilated;

    [DataField]
    public bool AnnouncedMelding;
}

/// <summary>
/// One level of the Early Warning Joe's progression.
/// </summary>
[DataDefinition]
public sealed partial class EWJoeLevelDef
{
    /// <summary>Localized level name (UNDERTOW, MURMUR, ...).</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Actions granted when this level is reached.</summary>
    [DataField]
    public List<EntProtoId> Actions = new();

    /// <summary>Components added when this level is reached. Existing components are not replaced.</summary>
    [DataField]
    public ComponentRegistry Components = new();
}

/// <summary>
/// The Early Warning Joe's level table. Kept as a prototype so balance lives in one YAML file and
/// tests can apply levels without running antag selection.
/// </summary>
[Prototype("ewJoeLevels")]
public sealed partial class EWJoeLevelsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Index = level. Level 0 is the baseline granted at selection.</summary>
    [DataField(required: true)]
    public List<EWJoeLevelDef> Levels = new();
}
