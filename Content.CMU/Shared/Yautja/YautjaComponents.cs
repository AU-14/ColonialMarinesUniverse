using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Speech;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Acid;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaComponent : Component
{
    [DataField]
    public LocId RankName = "cmu-yautja-rank-hunter";

    [DataField]
    public float BaseWalkSpeed = 4.4f;

    [DataField]
    public float BaseSprintSpeed = 8.4f;

    [DataField]
    public float UnarmedAttackRate = 1.15f;

    [DataField]
    public FixedPoint2 UnarmedBluntDamage = 12;

    [DataField]
    public FixedPoint2 UnarmedStructuralDamage = 3;

    [DataField]
    public int SkillLevel = 4;

    [DataField]
    public ProtoId<DamageModifierSetPrototype>? DamageModifierSet = "CMUYautja";

    [DataField]
    public ProtoId<SpeechSoundsPrototype>? SpeechSounds = "CMUYautjaSpeech";

    [DataField]
    public LocId IdentityName = "cmu-yautja-identity-unknown";

    [DataField]
    public bool RandomizeSkinColor = true;

    [ViewVariables]
    public bool SkinColorRandomized;

    [DataField]
    public List<ProtoId<EmotePrototype>> AllowedEmotes = GetDefaultAllowedEmotes();

    [DataField]
    public Dictionary<Sex, ProtoId<EmoteSoundsPrototype>> VocalSounds = GetDefaultVocalSounds();

    [DataField]
    public EntProtoId VoiceClickActionId = "CMUActionYautjaVoiceClick";

    [ViewVariables]
    public EntityUid? VoiceClickAction;

    [DataField]
    public EntProtoId VoiceRoarActionId = "CMUActionYautjaVoiceRoar";

    [ViewVariables]
    public EntityUid? VoiceRoarAction;

    [DataField]
    public EntProtoId VoiceLaughActionId = "CMUActionYautjaVoiceLaugh";

    [ViewVariables]
    public EntityUid? VoiceLaughAction;

    [DataField]
    public EntProtoId VoiceGrowlActionId = "CMUActionYautjaVoiceGrowl";

    [ViewVariables]
    public EntityUid? VoiceGrowlAction;

    [DataField]
    public EntProtoId VoicePainActionId = "CMUActionYautjaVoicePain";

    [ViewVariables]
    public EntityUid? VoicePainAction;

    [DataField]
    public EntProtoId VoiceDeathCryActionId = "CMUActionYautjaVoiceDeathCry";

    [ViewVariables]
    public EntityUid? VoiceDeathCryAction;

    [DataField]
    public EntProtoId VoiceDeathLaughActionId = "CMUActionYautjaVoiceDeathLaugh";

    [ViewVariables]
    public EntityUid? VoiceDeathLaughAction;

    private static List<ProtoId<EmotePrototype>> GetDefaultAllowedEmotes()
    {
        var emotes = new List<ProtoId<EmotePrototype>>();
        emotes.Add("Scream");
        emotes.Add("Laugh");
        emotes.Add("Growl");
        emotes.Add("Warcry");
        emotes.Add("CMUYautjaClick");
        emotes.Add("CMUYautjaRoar");
        emotes.Add("CMUYautjaLaugh");
        emotes.Add("CMUYautjaGrowl");
        emotes.Add("CMUYautjaPain");
        emotes.Add("CMUYautjaDeathCry");
        emotes.Add("CMUYautjaDeathLaugh");
        return emotes;
    }

    private static Dictionary<Sex, ProtoId<EmoteSoundsPrototype>> GetDefaultVocalSounds()
    {
        var sounds = new Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>();
        sounds.Add(Sex.Male, "CMUMaleYautja");
        sounds.Add(Sex.Female, "CMUFemaleYautja");
        sounds.Add(Sex.Unsexed, "CMUMaleYautja");
        return sounds;
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaPowerSystem), typeof(YautjaMaskSystem), typeof(YautjaCloakSystem), typeof(YautjaSelfDestructSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class YautjaBracerComponent : Component, IClothingSlots
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxCharge = 3000;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Charge = 3000;

    [DataField]
    public FixedPoint2 Regen = 2;

    [DataField]
    public TimeSpan RegenEvery = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NextRegen;

    [DataField]
    public ProtoId<AlertPrototype> PowerAlert = "CMUYautjaPower";

    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.GLOVES;

    [DataField]
    public EntityUid? User;

    [DataField]
    public EntProtoId ToggleCloakActionId = "CMUActionYautjaToggleCloak";

    [ViewVariables]
    public EntityUid? ToggleCloakAction;

    [DataField]
    public EntProtoId OpenMarkPanelActionId = "CMUActionYautjaOpenMarkPanel";

    [ViewVariables]
    public EntityUid? OpenMarkPanelAction;

    [DataField]
    public EntProtoId RecallActionId = "CMUActionYautjaRecall";

    [ViewVariables]
    public EntityUid? RecallAction;

    [DataField]
    public EntProtoId SelfDestructActionId = "CMUActionYautjaSelfDestruct";

    [ViewVariables]
    public EntityUid? SelfDestructAction;

    [DataField]
    public EntProtoId ToggleLockActionId = "CMUActionYautjaToggleBracerLock";

    [ViewVariables]
    public EntityUid? ToggleLockAction;

    [DataField]
    public EntProtoId ToggleIdChipActionId = "CMUActionYautjaToggleBracerIdChip";

    [ViewVariables]
    public EntityUid? ToggleIdChipAction;

    [DataField]
    public EntProtoId CreateStabilisingCrystalActionId = "CMUActionYautjaCreateStabilisingCrystal";

    [ViewVariables]
    public EntityUid? CreateStabilisingCrystalAction;

    [DataField]
    public EntProtoId CreateHumanStabilisingCrystalActionId = "CMUActionYautjaCreateHumanStabilisingCrystal";

    [ViewVariables]
    public EntityUid? CreateHumanStabilisingCrystalAction;

    [DataField]
    public EntProtoId CreateHealingCapsuleActionId = "CMUActionYautjaCreateHealingCapsule";

    [ViewVariables]
    public EntityUid? CreateHealingCapsuleAction;

    [DataField]
    public EntProtoId LinkThrallBracerActionId = "CMUActionYautjaLinkThrallBracer";

    [ViewVariables]
    public EntityUid? LinkThrallBracerAction;

    [DataField]
    public EntProtoId TransmitThrallMessageActionId = "CMUActionYautjaTransmitThrallMessage";

    [ViewVariables]
    public EntityUid? TransmitThrallMessageAction;

    [DataField]
    public EntProtoId StunThrallActionId = "CMUActionYautjaStunThrall";

    [ViewVariables]
    public EntityUid? StunThrallAction;

    [DataField]
    public EntProtoId SelfDestructThrallActionId = "CMUActionYautjaSelfDestructThrall";

    [ViewVariables]
    public EntityUid? SelfDestructThrallAction;

    [DataField, AutoNetworkedField]
    public bool Locked = true;

    [DataField]
    public string IdChipContainerId = "cmu-yautja-id-chip";

    [DataField]
    public EntProtoId IdChipPrototype = "CMUYautjaBracerIdChip";

    [DataField]
    public EntityUid? IdChip;

    [DataField, AutoNetworkedField]
    public bool IdChipDeployed;

    [DataField]
    public EntProtoId StabilisingCrystalPrototype = "CMUYautjaStabilisingCrystal";

    [DataField]
    public EntProtoId HumanStabilisingCrystalPrototype = "CMUYautjaHumanStabilisingCrystal";

    [DataField]
    public EntProtoId HealingCapsulePrototype = "CMUYautjaHealingCapsule";

    [DataField]
    public FixedPoint2 StabilisingCrystalCost = 400;

    [DataField]
    public FixedPoint2 HumanStabilisingCrystalCost = 400;

    [DataField]
    public FixedPoint2 HealingCapsuleCost = 600;

    [DataField]
    public TimeSpan StabilisingCrystalCooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan HealingCapsuleCooldown = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan NextStabilisingCrystal;

    [DataField]
    public TimeSpan NextHealingCapsule;

    [DataField, AutoNetworkedField]
    public bool SelfDestructArmed;

    [DataField, AutoNetworkedField]
    public TimeSpan SelfDestructAt;

    [DataField]
    public TimeSpan NextSelfDestructWarning;

    [DataField]
    public TimeSpan SelfDestructDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public ProtoId<ExplosionPrototype> SelfDestructExplosion = "RMCOB";

    [DataField]
    public float SelfDestructTotalIntensity = 2450;

    [DataField]
    public float SelfDestructIntensitySlope = 10;

    [DataField]
    public float SelfDestructMaxIntensity = 98;

    [DataField]
    public int SelfDestructMaxTileBreak = 3;

    [DataField]
    public TimeSpan SelfDestructWarningEvery = TimeSpan.FromSeconds(1);

    [DataField]
    public float SelfDestructGibSplatModifier = 5f;

    [DataField]
    public float SelfDestructEquipmentDestroyRadius = 2f;

    [DataField]
    public SoundSpecifier EquipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public SoundSpecifier CloakOnSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_cloakon.wav");

    [DataField]
    public SoundSpecifier CloakOffSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_cloakoff.wav");

    [DataField]
    public float CloakOpacity = 0.02f;

    [DataField]
    public bool CloakRestrictWeapons = true;

    [DataField]
    public bool CloakHideNightVision = true;

    [DataField]
    public bool CloakBlockFriendlyFire = true;

    [DataField]
    public TimeSpan CloakUncloakWeaponLock = TimeSpan.FromSeconds(1);

    [DataField]
    public EntProtoId CloakEffect = "RMCEffectCloak";

    [DataField]
    public EntProtoId UncloakEffect = "RMCEffectUncloak";

    [DataField]
    public HashSet<HumanoidVisualLayers> CloakedHideLayers = new()
    {
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.Eyes,
    };

    [DataField]
    public SoundSpecifier RecallSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_attach.wav");

    [DataField]
    public SoundSpecifier SelfDestructArmSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_countdown.ogg", AudioParams.Default.WithVolume(8f).WithMaxDistance(40f));

    [DataField]
    public SoundSpecifier SelfDestructCancelSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_plasmacaster_off.wav");

    [DataField]
    public SoundSpecifier SelfDestructWarningSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav", AudioParams.Default.WithVolume(6f).WithMaxDistance(35f));

    [DataField]
    public SoundSpecifier SelfDestructLaughSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_deathlaugh.wav", AudioParams.Default.WithVolume(8f).WithMaxDistance(40f));

    [DataField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public SoundSpecifier IdChipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_attach.wav");

    [DataField]
    public SoundSpecifier FabricateSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public DamageSpecifier TechShockDamage = new()
    {
        DamageDict = new()
        {
            { "Shock", 20 },
        },
    };

    [DataField]
    public TimeSpan TechShockStun = TimeSpan.FromSeconds(2);

    [DataField]
    public float NonYautjaWorkingChance = 0.20f;

    [DataField]
    public float NonYautjaRandomFunctionChance = 0.10f;

    [DataField]
    public float ResearcherWorkingChance = 0.25f;

    [DataField]
    public float ResearcherRandomFunctionChance = 0.07f;

    [DataField]
    public float SynthWorkingChance = 0.40f;

    [DataField]
    public float SynthRandomFunctionChance = 0.04f;

    [DataField]
    public float NonYautjaDelimbChance = 0.08f;

    [DataField]
    public TimeSpan NonYautjaCloakShockEvery = TimeSpan.FromSeconds(2);

    [DataField]
    public float NonYautjaCloakShockChance = 0.25f;

    [DataField]
    public TimeSpan NextNonYautjaCloakShock;

    [DataField]
    public float BulletDecloakChance = 0.20f;

    [DataField]
    public bool BulletDecloakAbsorbs = true;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaMaskSystem))]
public sealed partial class YautjaMaskComponent : Component, IClothingSlots
{
    [DataField]
    public EntProtoId ToggleVisorActionId = "CMUActionYautjaToggleVisor";

    [ViewVariables]
    public EntityUid? ToggleVisorAction;

    [DataField]
    public EntProtoId ToggleZoomActionId = "CMUActionYautjaToggleMaskZoom";

    [ViewVariables]
    public EntityUid? ToggleZoomAction;

    [DataField, AutoNetworkedField]
    public bool VisorEnabled;

    [DataField, AutoNetworkedField]
    public bool Zoomed;

    [DataField]
    public float ZoomLevel = 0.45f;

    [DataField]
    public float ZoomOffset = 14f;

    [DataField]
    public EntityUid? User;

    [DataField]
    public FixedPoint2 Drain = 3;

    [DataField]
    public TimeSpan DrainEvery = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan NextDrain;

    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.MASK;

    [DataField]
    public SoundSpecifier ToggleVisorSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_vision.wav");

    [DataField]
    public SoundSpecifier ZoomOnSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_vision.wav");

    [DataField]
    public SoundSpecifier ZoomOffSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_vision.wav");
}

[RegisterComponent]
public sealed partial class YautjaPowerActionComponent : Component
{
    [DataField]
    public FixedPoint2 Cost;

    [DataField]
    public bool RequireBracer = true;

    [DataField]
    public bool RequireMask;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaHudViewerComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaMaskSystem))]
public sealed partial class YautjaMaskZoomComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Mask;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaMarkSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class YautjaThrallComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Master;

    [DataField, AutoNetworkedField]
    public string Reason = string.Empty;

    [DataField, AutoNetworkedField]
    public bool BracerLinked;

    [DataField, AutoNetworkedField]
    public EntityUid? MasterBracer;

    [DataField, AutoNetworkedField]
    public EntityUid? ThrallBracer;

    [DataField, AutoNetworkedField]
    public bool Blooded;

    [DataField, AutoNetworkedField]
    public bool TechAuthorized;

    [DataField, AutoNetworkedField]
    public bool Hivebroken;

    [DataField]
    public bool HivebreakOriginalStateCaptured;

    [DataField]
    public EntityUid? HivebreakOriginalHive;

    [DataField]
    public bool HivebreakHadNpcFaction;

    [DataField]
    public HashSet<ProtoId<NpcFactionPrototype>> HivebreakOriginalNpcFactions = new();

    [DataField]
    public bool HivebreakHadUserIff;

    [DataField]
    public HashSet<EntProtoId<IFFFactionComponent>> HivebreakOriginalIffFactions = new();

    [DataField]
    public bool HivebreakHadIgnoreWeedsSlowdown;

    [DataField]
    public bool HivebreakHadSpeech;

    [DataField]
    public ProtoId<SpeechVerbPrototype>? HivebreakOriginalSpeechVerb;

    [DataField]
    public ProtoId<SpeechSoundsPrototype>? HivebreakOriginalSpeechSounds;

    [DataField]
    public bool HivebreakHadXenoRegen;

    [DataField]
    public bool HivebreakOriginalHealOffWeeds;

    [DataField]
    public bool HivebreakHadHivebrokenName;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaTechAuthorizedComponent : Component;

[RegisterComponent]
public sealed partial class YautjaHivebrokenXenoComponent : Component;

[RegisterComponent]
public sealed partial class YautjaHealingGunComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public float BloodlossModifier;

    [DataField]
    public float ModifyBloodLevel;

    [DataField]
    public List<ProtoId<DamageContainerPrototype>>? DamageContainers;

    [DataField]
    public SoundSpecifier? HealSound;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaMarkSystem))]
public sealed partial class YautjaMarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<YautjaMarkKind, EntityUid> Marks = new();
}

[RegisterComponent]
public sealed partial class YautjaAbominationHostComponent : Component
{
    [DataField]
    public EntProtoId LarvaPrototype = "CMUXenoPredalienLarva";
}

[RegisterComponent]
public sealed partial class YautjaAbominationLarvaComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaAbominationComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Kills;

    [DataField]
    public int MaxKills = 10;

    [DataField]
    public FixedPoint2 DamagePerKill = 2.5;

    [DataField]
    public float YautjaDamageMultiplier = 1.5f;

    [DataField, AutoNetworkedField]
    public bool FrenzyAreaMode;

    [DataField, AutoNetworkedField]
    public bool Announced;

    [DataField]
    public TimeSpan AnnounceDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan AnnounceAt;

    [DataField]
    public float RoarRange = 7f;

    [DataField]
    public float FrenzyRange = 2f;

    [DataField]
    public float SmashRange = 4f;

    [DataField]
    public FixedPoint2 SmashBaseDamage = 20;

    [DataField]
    public FixedPoint2 SmashDamagePerKill = 10;

    [DataField]
    public FixedPoint2 FrenzySingleBaseDamage = 25;

    [DataField]
    public FixedPoint2 FrenzyAreaBaseDamage = 15;

    [DataField]
    public FixedPoint2 FrenzyDamagePerKill = 10;

    [DataField]
    public TimeSpan SmashParalyze = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan RushDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public float RushSpeedMultiplier = 1.35f;

    [DataField]
    public FixedPoint2 RoarDamagePerKill = 2.5;

    [DataField]
    public float RoarSpeedPerKill = 0.05f;

    [DataField]
    public TimeSpan RoarBuffBaseDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan RoarBuffDurationPerKill = TimeSpan.FromSeconds(0.25);

    [DataField]
    public SoundSpecifier RoarSound = new SoundCollectionSpecifier("CMUPredalienRoar");

    [DataField]
    public SoundSpecifier RushSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/predalien_click.ogg");

    [DataField]
    public SoundSpecifier SmashSound = new SoundCollectionSpecifier("CMUYautjaSlam");

    [DataField]
    public SoundSpecifier FrenzySound = new SoundCollectionSpecifier("RCMXenoClaw");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaAbominationRushComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.35f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaAbominationRoarBuffComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public FixedPoint2 DamageBonus;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaRecallableComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? YautjaOwner;

    [DataField]
    public float Range = 10f;
}

[RegisterComponent]
public sealed partial class YautjaSmartDiscComponent : Component
{
    [DataField]
    public float SearchRange = 8f;

    [DataField]
    public float ThrowSpeed = 13f;

    [DataField]
    public float SpinVelocity = 24f;

    [DataField]
    public int MaxHits = 3;

    [DataField]
    public float HitRange = 0.7f;

    [DataField]
    public TimeSpan HitDelay = TimeSpan.FromSeconds(0.45);

    [DataField]
    public TimeSpan ActiveTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan RetargetDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/star_hit.ogg");

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Active;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? YautjaOwner;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? CurrentTarget;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? RogueTarget;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? RogueActivator;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Hits;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ActiveUntil;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextRetarget;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextHit;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaCasterSystem))]
public sealed partial class YautjaCasterComponent : Component
{
    [DataField]
    public FixedPoint2 PowerCost = 100;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_plasmacaster_fire.wav");

    [DataField]
    public List<YautjaCasterMode> Modes = new();

    [DataField, AutoNetworkedField]
    public int CurrentMode;
}

[DataDefinition]
public sealed partial class YautjaCasterMode
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Projectile = default!;

    [DataField]
    public FixedPoint2 PowerCost = 100;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_plasmacaster_fire.wav");
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaTechItemComponent : Component
{
    [DataField]
    public bool BlockPickup = true;

    [DataField]
    public bool BlockUse = true;

    [DataField]
    public bool BlockMelee = true;

    [DataField]
    public bool BlockThrow = true;

    [DataField]
    public bool BlockShoot = true;
}

[ByRefEvent]
public record struct YautjaTechMisusedEvent(EntityUid User, EntityUid Item, YautjaTechMisuseKind Kind);

[RegisterComponent]
public sealed partial class YautjaBracerIdChipComponent : Component;

[RegisterComponent]
public sealed partial class YautjaCleanerComponent : Component
{
    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan DissolveDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public EntProtoId AcidPrototype = "CMUYautjaCleanserAcid";

    [DataField]
    public XenoAcidStrength AcidStrength = XenoAcidStrength.Strong;

    [DataField]
    public float AcidDps = 0;

    [DataField]
    public float LightAcidDps = 0;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/acid_sizzle1.ogg");

    [DataField]
    public SoundSpecifier FinishSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/acid_sizzle4.ogg");
}

[RegisterComponent]
public sealed partial class YautjaDissolvingComponent : Component
{
    [DataField]
    public TimeSpan DeleteAt;
}

[RegisterComponent]
public sealed partial class YautjaHivebreakerComponent : Component
{
    [DataField]
    public int Uses = 1;

    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(3);

    [DataField]
    public bool RequireCritical = true;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public SoundSpecifier FinishSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_roar1.wav");

    [DataField]
    public bool BloodOnConversion = true;

    [DataField]
    public bool AuthorizeTechOnConversion = true;

    [DataField]
    public bool ClearHiveOnConversion = true;

    [DataField]
    public bool HealOnConversion = true;

    [DataField]
    public bool IgnoreWeedSlowdownOnConversion = true;

    [DataField]
    public bool HumanSpeechOnConversion = true;

    [DataField]
    public ProtoId<SpeechVerbPrototype> HumanSpeechVerb = "Default";

    [DataField]
    public ProtoId<SpeechSoundsPrototype> HumanSpeechSounds = "Bass";

    [DataField]
    public ProtoId<NpcFactionPrototype> XenoNpcFaction = "RMCXeno";

    [DataField]
    public ProtoId<NpcFactionPrototype> ThrallNpcFaction = "CMUYautja";

    [DataField]
    public EntProtoId<IFFFactionComponent> XenoIffFaction = "FactionXeno";

    [DataField]
    public EntProtoId<IFFFactionComponent> ThrallIffFaction = "FactionYautja";
}

[RegisterComponent]
public sealed partial class YautjaRelayBeaconComponent : Component
{
    [DataField]
    public SoundSpecifier PulseSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");
}

[RegisterComponent]
public sealed partial class YautjaHoundPadComponent : Component
{
    [DataField]
    public SoundSpecifier PulseSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_vision.wav");
}

[RegisterComponent]
public sealed partial class YautjaScalpComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaPowerSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class YautjaThrallBracerComponent : Component, IClothingSlots
{
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.GLOVES;

    [DataField]
    public EntityUid? User;

    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    [DataField, AutoNetworkedField]
    public EntityUid? MasterBracer;

    [DataField, AutoNetworkedField]
    public bool Linked;

    [DataField, AutoNetworkedField]
    public bool Locked;

    [DataField]
    public EntProtoId TransmitThrallMessageActionId = "CMUActionYautjaTransmitThrallMessage";

    [ViewVariables]
    public EntityUid? TransmitThrallMessageAction;

    [DataField, AutoNetworkedField]
    public bool SelfDestructArmed;

    [DataField, AutoNetworkedField]
    public TimeSpan SelfDestructAt;

    [DataField]
    public TimeSpan NextSelfDestructWarning;

    [DataField]
    public TimeSpan SelfDestructDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public ProtoId<ExplosionPrototype> SelfDestructExplosion = "RMC";

    [DataField]
    public float SelfDestructTotalIntensity = 500;

    [DataField]
    public float SelfDestructIntensitySlope = 10;

    [DataField]
    public float SelfDestructMaxIntensity = 65;

    [DataField]
    public int SelfDestructMaxTileBreak = 1;

    [DataField]
    public DamageSpecifier ShockDamage = new()
    {
        DamageDict = new()
        {
            { "Shock", 10 },
        },
    };

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(10);

    [DataField]
    public SoundSpecifier EquipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public SoundSpecifier LinkSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public SoundSpecifier MessageSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");

    [DataField]
    public SoundSpecifier ShockSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg");

    [DataField]
    public SoundSpecifier SelfDestructWarningSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");
}

[RegisterComponent]
public sealed partial class YautjaMaskAccessoryHolderComponent : Component
{
    [DataField]
    public string ContainerId = "cmu-yautja-mask-accessory";

    public ContainerSlot? Container;
}

[RegisterComponent]
public sealed partial class YautjaMaskOrnamentComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaGearContainerComponent : Component, IClothingSlots
{
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.GLOVES;

    [DataField]
    public string ContainerId = "cmu-yautja-gear";

    public Container? Container;

    [DataField]
    public EntProtoId ToggleCasterActionId = "CMUActionYautjaToggleCaster";

    [ViewVariables]
    public EntityUid? ToggleCasterAction;

    [DataField]
    public EntProtoId ToggleWristBladesActionId = "CMUActionYautjaToggleWristBlades";

    [ViewVariables]
    public EntityUid? ToggleWristBladesAction;

    [DataField]
    public EntProtoId ToggleScimitarActionId = "CMUActionYautjaToggleScimitar";

    [ViewVariables]
    public EntityUid? ToggleScimitarAction;

    [DataField]
    public EntProtoId ToggleShieldActionId = "CMUActionYautjaToggleShield";

    [ViewVariables]
    public EntityUid? ToggleShieldAction;

    [DataField]
    public EntProtoId ToggleChainGauntletActionId = "CMUActionYautjaToggleChainGauntlet";

    [ViewVariables]
    public EntityUid? ToggleChainGauntletAction;

    [DataField]
    public Dictionary<YautjaGearKind, EntProtoId> GearPrototypes = GetDefaultGearPrototypes();

    [DataField]
    public SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_attach.wav");

    [DataField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_attach.wav");

    [DataField]
    public SoundSpecifier CasterDeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_plasmacaster_on.wav");

    [DataField]
    public SoundSpecifier CasterRetractSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_plasmacaster_off.wav");

    [DataField]
    public SoundSpecifier WristBladesDeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/wristblades_on.wav");

    [DataField]
    public SoundSpecifier WristBladesRetractSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/wristblades_off.wav");

    [DataField]
    public Dictionary<YautjaGearKind, EntityUid> Gear = new();

    private static Dictionary<YautjaGearKind, EntProtoId> GetDefaultGearPrototypes()
    {
        var gear = new Dictionary<YautjaGearKind, EntProtoId>();
        gear.Add(YautjaGearKind.Caster, "CMUYautjaPlasmaCaster");
        gear.Add(YautjaGearKind.WristBlades, "CMUYautjaWristBlades");
        gear.Add(YautjaGearKind.Scimitar, "CMUYautjaScimitar");
        gear.Add(YautjaGearKind.Shield, "CMUYautjaBracerShield");
        gear.Add(YautjaGearKind.ChainGauntlet, "CMUYautjaChainGauntlet");
        return gear;
    }
}

[RegisterComponent]
public sealed partial class YautjaStoredGearComponent : Component
{
    [DataField]
    public EntityUid? Bracer;

    [DataField]
    public YautjaGearKind Kind;

    [DataField]
    public bool Deployed;

    public bool Retracting;
}

[RegisterComponent]
public sealed partial class YautjaTrophySourceComponent : Component
{
    [DataField]
    public TimeSpan HarvestDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier HarvestStartSound = new SoundCollectionSpecifier("gib");

    [DataField]
    public SoundSpecifier HarvestFinishSound = new SoundCollectionSpecifier("blood");

    [DataField]
    public int ButcheryProgress;

    [DataField]
    public SoundSpecifier ButcherStartSound = new SoundCollectionSpecifier("gib");

    [DataField]
    public SoundSpecifier ButcherFinishSound = new SoundCollectionSpecifier("blood");

    [DataField]
    public HashSet<YautjaTrophyKind> TakenTrophies = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaTrophyComponent : Component
{
    [DataField, AutoNetworkedField]
    public YautjaTrophyKind Kind;

    [DataField, AutoNetworkedField]
    public EntityUid? Source;

    [DataField, AutoNetworkedField]
    public EntityUid? Hunter;

    [DataField, AutoNetworkedField]
    public string SourceName = string.Empty;

    [DataField, AutoNetworkedField]
    public bool Polished;
}

[RegisterComponent]
public sealed partial class YautjaTrophyRecordComponent : Component
{
    [DataField]
    public int HumanSkulls;

    [DataField]
    public int HumanBones;

    [DataField]
    public int XenoSkulls;

    [DataField]
    public int XenoPelts;

    [DataField]
    public int PolishedTrophies;

    [DataField]
    public int RitualDuelWins;

    [DataField]
    public int Score;

    [DataField]
    public LocId RankName = "cmu-yautja-rank-hunter";
}

[RegisterComponent]
public sealed partial class YautjaTrophyDisplayComponent : Component;

[RegisterComponent]
public sealed partial class YautjaPolishingRagComponent : Component;

[RegisterComponent]
public sealed partial class YautjaRitualDuelComponent : Component
{
    [DataField]
    public EntityUid Hunter;

    [DataField]
    public YautjaRitualState State = YautjaRitualState.Captive;

    [DataField]
    public TimeSpan CapturedAt;

    [DataField]
    public TimeSpan DuelStartedAt;

    [DataField]
    public SoundSpecifier ClaimSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_roar1.wav");

    [DataField]
    public SoundSpecifier DuelSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_roar2.wav");

    [DataField]
    public SoundSpecifier ReleaseSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_click1.wav");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField, AutoNetworkedField]
    public EntityUid? TrapOwner;

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 20 },
            { "Slash", 15 },
        },
    };

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_attach.wav");

    [DataField]
    public SoundSpecifier DisarmSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_attach.wav");

    [DataField]
    public SoundSpecifier TriggerSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
}

[Serializable, NetSerializable]
public enum YautjaTrophyKind : byte
{
    HumanSkull,
    HumanLeftArmBone,
    HumanRightArmBone,
    HumanLeftHandBone,
    HumanRightHandBone,
    HumanLeftLegBone,
    HumanRightLegBone,
    HumanLeftFootBone,
    HumanRightFootBone,
    HumanRibcage,
    XenoSkull,
    XenoPelt,
}

[Serializable, NetSerializable]
public enum YautjaRitualState : byte
{
    Captive,
    DuelActive,
}

[Serializable, NetSerializable]
public enum YautjaMarkKind : byte
{
    Prey,
    Honored,
    Dishonored,
    GearCarrier,
    Thrall,
    Student,
    Blooded,
}

[Serializable, NetSerializable]
public enum YautjaGearKind : byte
{
    Caster,
    WristBlades,
    Scimitar,
    Shield,
    ChainGauntlet,
}

[Serializable, NetSerializable]
public enum YautjaButcherKind : byte
{
    Human,
    Xeno,
}

[Serializable, NetSerializable]
public enum YautjaTechMisuseKind : byte
{
    Pickup,
    Use,
    Melee,
    Throw,
    Shoot,
}
