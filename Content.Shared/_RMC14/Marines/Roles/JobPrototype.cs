using Content.Shared._RMC14.Item;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Medal;
using Content.Shared._RMC14.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Content.Shared._AU14.Marines.Roles.Chevrons;
using Content.Shared._CMU14.Round.Roles;
using Robust.Shared.GameObjects;

// ReSharper disable CheckNamespace
namespace Content.Shared.Roles;
// ReSharper restore CheckNamespace

public sealed partial class JobPrototype : IInheritingPrototype, ICMSpecific
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<JobPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField]
    public bool IsCM { get; private set; }

    [DataField]
    public bool HasSquad;

    [DataField]
    public bool HasIcon = true;

    [DataField]
    public bool Hidden;

    /// <summary>
    /// Whether this job represents a synthetic (android) role. Characters must have
    /// <see cref="Content.Shared.Preferences.HumanoidCharacterProfile.Synthetic"/> set to
    /// match this before they can be resolved into the role.
    /// </summary>
    [DataField]
    public bool IsSynthetic;

    [DataField]
    public int? OverwatchSortPriority;

    [DataField]
    public bool OverwatchShowName;

    [DataField]
    public string? OverwatchRoleName;

    [DataField]
    public string? SpawnMenuRoleName;

    [DataField]
    public string? NewToJobInfo;

    [DataField]
    public Dictionary<ProtoId<RankPrototype>, HashSet<JobRequirement>?>? Ranks;

    [DataField]
    public Dictionary<string, ChevronDefinition>? Chevrons { get; set; }

    [DataField]
    public Dictionary<RMCPlaytimeMedalType, EntProtoId>? Medals;

    [DataField]
    public float RoleWeight;

    [DataField]
    public ProtoId<StartingGearPrototype>? DummyStartingGear { get; private set; }

    [DataField]
    public LocId? Greeting;

    /// <summary>
    /// RMC14 for arrival notification sound if <see cref="JoinNotifyCrew"/> true.
    /// </summary>
    [DataField]
    public SoundSpecifier LatejoinArrivalSound { get; private set; } = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/sound_misc_boatswain.ogg");

    /// <summary>
    /// This field logically identifies the level in the Marine command hierarchy when delegating the Operation Commander's authority.
    /// A value of 0 implies that is not a Marine or Marine is not eligible to assume Operation Commander's authority.
    /// </summary>
    [DataField]
    public int MarineAuthorityLevel { get; private set; } = 0;

    [DataField]
    public ProtoId<JobPrototype>? UseLoadoutOfJob;

    [DataField]
    [NeverPushInheritance]
    public bool BasePlaytimeTracker;

    [DataField]
    public ProtoId<JobPrototype>? WhitelistParent;

    /// <summary>
    /// Starting gear that is given when the map has a certain camoflage enabled.
    /// </summary>
    [DataField]
    public Dictionary<CamouflageType, ProtoId<StartingGearPrototype>>? CamouflageStartingGear;

    /// <summary>
    /// If the mob (e.g. working-joe) should receive a name change and appearance from the loadout.
    /// </summary>
    [DataField]
    public bool UsePlayerProfile = true;

    /// <summary>
    /// If we would like to grab the AddComponentSpecials from the parent prototypes.
    /// Used for military job/factions to reduce bloat.
    /// </summary>
    [DataField]
    public bool InheritAddComponentSpecials = false;

    /// <summary>
    /// Explicit round-side metadata used for AU14/CMU side routing.
    /// </summary>
    [DataField]
    public RoundJobSide RoundSide { get; private set; } = RoundJobSide.None;

    /// <summary>
    /// Force key within the round side, such as GOVFOR, OPFOR, WYPMC, CMB, ICRC, or CLF.
    /// </summary>
    [DataField]
    public string? RoundForce { get; private set; }

    /// <summary>
    /// Stable role family key, such as SquadRifleman or JuniorOfficer.
    /// </summary>
    [DataField]
    public string? RoundRole { get; private set; }

    /// <summary>
    /// Reusable role profiles applied before <see cref="RoundComponents"/>.
    /// Use this for faction/family defaults so individual job files only need their unique overlays.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public List<ProtoId<RoundJobProfilePrototype>> RoundProfiles { get; private set; } = new();

    /// <summary>
    /// Job-local round components applied after shared round profiles.
    /// Prefer this for one-off role data instead of creating a separate per-job profile prototype.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public ComponentRegistry RoundComponents { get; private set; } = new();

    /// <summary>
    /// Job-local components applied when <see cref="RoundSide"/> matches the dictionary key.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public Dictionary<string, ComponentRegistry> RoundSideComponents { get; private set; } = new();

    /// <summary>
    /// Job-local components applied when <see cref="RoundForce"/> matches the dictionary key.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public Dictionary<string, ComponentRegistry> RoundForceComponents { get; private set; } = new();

    /// <summary>
    /// Whether <see cref="RoundComponents"/> replaces existing components when applied.
    /// </summary>
    [DataField]
    public bool RoundComponentsRemoveExisting { get; private set; } = true;
}
