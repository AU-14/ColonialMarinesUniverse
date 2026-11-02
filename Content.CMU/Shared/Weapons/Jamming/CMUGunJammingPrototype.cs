using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.CMU14.Weapons.Jamming;

/// <summary>
///     Balance settings for gun wear, jamming, clearing jams and maintenance. Every gun uses
///     <see cref="CMUGunJammingSystem.DefaultProfile"/> unless its <see cref="CMUGunJammingComponent"/> names another
///     profile, so editing the default profile changes every gun at once. Profiles can inherit from each other.
/// </summary>
/// <remarks>
///     Wear is counted from 0 up to <see cref="MaxWear"/>. Values called fractions are a share of
///     <see cref="MaxWear"/>, from 0 to 1.
/// </remarks>
[Prototype("cmuGunJamming")]
public sealed partial class CMUGunJammingPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CMUGunJammingPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     With this off, guns on this profile don't wear, can't jam, and fire even if they were jammed.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    ///     Only read from the default profile: whether handheld guns get wear and jamming automatically.
    ///     Guns with <see cref="CMUGunJammingComponent"/> in their prototype always have it.
    /// </summary>
    [DataField]
    public bool AutoApply = true;

    /// <summary>
    ///     Only read from the default profile: handheld guns that get wear and jamming automatically. Null means all.
    /// </summary>
    [DataField]
    public EntityWhitelist? AutoApplyWhitelist;

    /// <summary>
    ///     Only read from the default profile: handheld guns that never get wear and jamming automatically.
    /// </summary>
    [DataField]
    public EntityWhitelist? AutoApplyBlacklist;

    [DataField]
    public float MaxWear = 100f;

    /// <summary>
    ///     Wear added by each round fired, before any multipliers.
    /// </summary>
    [DataField]
    public float BaseWearPerShot = 0.05f;

    [DataField]
    public float SemiAutoWearMultiplier = 1f;

    [DataField]
    public float BurstWearMultiplier = 1.35f;

    [DataField]
    public float FullAutoWearMultiplier = 1.75f;

    /// <summary>
    ///     Guns with any of these tags count as shotguns.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> ShotgunTags = new();

    /// <summary>
    ///     Extra wear multiplier for shotgun rounds that weren't fired by working a pump action.
    /// </summary>
    [DataField]
    public float ShotgunSemiAutoWearMultiplier = 1.5f;

    /// <summary>
    ///     Wear multipliers for special ammo, matched against the cartridge and projectile prototype IDs.
    ///     A key matches when the ID contains it, case-sensitively; when several match, the largest wins.
    ///     Ammo nothing matches is standard and counts as 1.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public Dictionary<string, float> AmmoIdKeywords = new();

    /// <summary>
    ///     Wear multipliers for ammo whose cartridge or projectile prototype has one of these tags.
    ///     Combined with <see cref="AmmoIdKeywords"/>; the largest match wins.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public Dictionary<ProtoId<TagPrototype>, float> AmmoTagMultipliers = new();

    /// <summary>
    ///     Below this fraction of max wear, the gun never jams.
    /// </summary>
    [DataField]
    public float JamWearThreshold = 0.1f;

    /// <summary>
    ///     Jam chance per round fired just above <see cref="JamWearThreshold"/>.
    /// </summary>
    [DataField]
    public float MinJamChance = 0.0005f;

    /// <summary>
    ///     Jam chance per round fired at max wear.
    /// </summary>
    [DataField]
    public float MaxJamChance = 0.05f;

    /// <summary>
    ///     Shapes the climb from <see cref="MinJamChance"/> to <see cref="MaxJamChance"/>. 1 is a straight line;
    ///     higher numbers keep the chance low until the gun is badly worn.
    /// </summary>
    [DataField]
    public float JamChanceExponent = 2f;

    /// <summary>
    ///     The kinds of jam a gun can get, how likely each is, and how each is cleared.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public Dictionary<CMUGunJamType, CMUGunJamTypeData> JamTypes = new();

    /// <summary>
    ///     Played when the trigger is pulled on a jammed gun. Null uses the gun's empty click.
    /// </summary>
    [DataField]
    public SoundSpecifier? JammedSound;

    [DataField]
    public SoundSpecifier? CycleSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Guns/Cock/gun_cocked2.ogg");

    [DataField]
    public bool SlamEnabled = true;

    [DataField]
    public TimeSpan SlamDelay = TimeSpan.FromSeconds(0.6);

    /// <summary>
    ///     Wear added by each stock slam, scaled by the gun's wear multiplier.
    /// </summary>
    [DataField]
    public float SlamWear = 5f;

    /// <summary>
    ///     Chance a stock slam destroys a gun with no wear.
    /// </summary>
    [DataField]
    public float SlamBreakChance = 0.02f;

    /// <summary>
    ///     Chance a stock slam destroys a gun at max wear.
    /// </summary>
    [DataField]
    public float SlamBreakChanceAtMaxWear = 0.3f;

    /// <summary>
    ///     Shapes the climb from <see cref="SlamBreakChance"/> to <see cref="SlamBreakChanceAtMaxWear"/>.
    /// </summary>
    [DataField]
    public float SlamBreakWearExponent = 1.5f;

    [DataField]
    public SoundSpecifier? SlamSound = new SoundPathSpecifier("/Audio/Effects/metal_thud1.ogg");

    [DataField]
    public SoundSpecifier? BreakSound = new SoundPathSpecifier("/Audio/Effects/metal_crunch.ogg");

    /// <summary>
    ///     How often the jammed and broken messages can show while the trigger is held.
    /// </summary>
    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Condition lines shown on examine. The tier with the highest threshold at or below the gun's wear fraction
    ///     is shown.
    /// </summary>
    [DataField]
    public List<CMUGunWearTier> ConditionTiers = new();
}

[DataDefinition]
public sealed partial class CMUGunJamTypeData
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    ///     How likely this jam is compared to the others.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    ///     How long cycling the gun to clear this jam takes.
    /// </summary>
    [DataField]
    public TimeSpan CycleDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Chance a finished cycle clears the jam.
    /// </summary>
    [DataField]
    public float CycleClearChance = 1f;

    [DataField]
    public CMUGunJamRoundLoss CycleRoundLoss = CMUGunJamRoundLoss.None;

    /// <summary>
    ///     Chance a stock slam that doesn't fire or break the gun clears the jam.
    /// </summary>
    [DataField]
    public float SlamClearChance = 0.85f;

    /// <summary>
    ///     Chance a stock slam sets off a round, firing it into the air and clearing the jam.
    /// </summary>
    [DataField]
    public float SlamDischargeChance;

    [DataField]
    public float SlamBreakChanceMultiplier = 1f;
}

[DataDefinition]
public sealed partial class CMUGunWearTier
{
    /// <summary>
    ///     Lowest wear fraction this tier covers.
    /// </summary>
    [DataField(required: true)]
    public float Threshold;

    [DataField(required: true)]
    public LocId Text;
}
