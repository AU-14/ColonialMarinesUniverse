using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Speech;
using Content.Shared.StatusIcon;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Acid;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
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
    public float StunResistance = 2f;

    [DataField]
    public float ShoveChanceBonus = 0.2f;

    [DataField]
    public Dictionary<FixedPoint2, float> SlowOnDamageThresholds = new()
    {
        { 160, 0.9f },
        { 240, 0.8f },
    };

    [DataField]
    public ProtoId<DamageModifierSetPrototype>? DamageModifierSet = "CMUYautja";

    [DataField]
    public ProtoId<SpeechSoundsPrototype>? SpeechSounds = "CMUYautjaSpeech";

    [DataField]
    public LocId IdentityName = "cmu-yautja-identity-unknown";

    [DataField]
    public bool BracerNameActive = true;

    [DataField]
    public bool RandomizeSkinColor = true;

    [DataField]
    public float SkinHueMin = 0f;

    [DataField]
    public float SkinHueMax = 1f;

    [DataField]
    public float SkinSaturationMin = 0f;

    [DataField]
    public float SkinSaturationMax = 1f;

    [DataField]
    public float SkinValueMin = 0f;

    [DataField]
    public float SkinValueMax = 1f;

    [ViewVariables]
    public bool SkinColorRandomized;

    [DataField]
    public EntProtoId LeapActionId = "CMUActionYautjaLeap";

    [ViewVariables]
    public EntityUid? LeapAction;

    [DataField]
    public EntProtoId MarkForHuntActionId = "CMUActionYautjaMarkForHunt";

    [ViewVariables]
    public EntityUid? MarkForHuntAction;

    [DataField]
    public EntProtoId OpenMarkPanelActionId = "CMUActionYautjaOpenMarkPanel";

    [ViewVariables]
    public EntityUid? OpenMarkPanelAction;

    [DataField]
    public EntProtoId ButcherActionId = "CMUActionYautjaButcher";

    [ViewVariables]
    public EntityUid? ButcherAction;

    [DataField]
    public float LeapThrowSpeed = 5f;

    [DataField]
    public float LeapMaxRange = 7f;

    [DataField]
    public TimeSpan LeapWindup = TimeSpan.FromSeconds(0.6);

    [DataField]
    public EntProtoId LeapWarningPrototype = "CMUYautjaLeapWarning";

    [DataField]
    public EntProtoId AudioPanelActionId = "CMUActionYautjaAudioPanel";

    [ViewVariables]
    public EntityUid? AudioPanelAction;

    [DataField]
    public TimeSpan AudioPanelCooldown = TimeSpan.FromSeconds(2.5);

    [ViewVariables]
    public TimeSpan NextAudioPanelEmote;

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
    public EntProtoId VoiceDistractActionId = "CMUActionYautjaVoiceDistract";

    [ViewVariables]
    public EntityUid? VoiceDistractAction;

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
        emotes.Add("CMUYautjaAudioClick");
        emotes.Add("CMUYautjaAudioClick2");
        emotes.Add("CMUYautjaAudioGrowl");
        emotes.Add("CMUYautjaAudioLaugh1");
        emotes.Add("CMUYautjaAudioLaugh2");
        emotes.Add("CMUYautjaAudioLaugh3");
        emotes.Add("CMUYautjaAudioLaugh4");
        emotes.Add("CMUYautjaAudioLaugh5");
        emotes.Add("CMUYautjaAudioLaugh6");
        emotes.Add("CMUYautjaAudioRoar");
        emotes.Add("CMUYautjaAudioRoar2");
        emotes.Add("CMUYautjaVoiceSynthAnytime");
        emotes.Add("CMUYautjaVoiceSynthHelpMe");
        emotes.Add("CMUYautjaVoiceSynthISeeYou");
        emotes.Add("CMUYautjaVoiceSynthItsATrap");
        emotes.Add("CMUYautjaVoiceSynthOverHere");
        emotes.Add("CMUYautjaVoiceSynthTurnAround");
        emotes.Add("CMUYautjaVoiceSynthComeOnOut");
        emotes.Add("CMUYautjaVoiceSynthOverThere");
        emotes.Add("CMUYautjaVoiceSynthUglyFreak");
        emotes.Add("CMUYautjaVoiceSynthLuckyYou");
        emotes.Add("CMUYautjaVoiceSynthJustYou");
        emotes.Add("CMUYautjaVoiceSynthTellMe");
        emotes.Add("CMUYautjaVoiceSynthDoItRookie");
        emotes.Add("CMUYautjaVoiceSynthForwardMarine");
        emotes.Add("CMUYautjaVoiceSynthBurnYouFucker");
        emotes.Add("CMUYautjaFakeAlienGrowl");
        emotes.Add("CMUYautjaFakeAlienHelp");
        emotes.Add("CMUYautjaFakeMaleScream");
        emotes.Add("CMUYautjaFakeFemaleScream");
        emotes.Add("CMUYautjaClick");
        emotes.Add("CMUYautjaRoar");
        emotes.Add("CMUYautjaLaugh");
        emotes.Add("CMUYautjaGrowl");
        emotes.Add("CMUYautjaPain");
        emotes.Add("CMUYautjaDistract");
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

[RegisterComponent]
public sealed partial class YautjaAppliedProfileComponent : Component
{
    public YautjaCharacterProfile Profile = YautjaCharacterProfile.Default;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class YautjaCapeComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = YautjaCharacterProfile.Default.CapeColor;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaYoungbloodComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Mentor;

    [DataField, AutoNetworkedField]
    public bool Blooded;

    [DataField, AutoNetworkedField]
    public EntityUid? BloodedBy;

    [DataField, AutoNetworkedField]
    public string BloodingReason = string.Empty;

    [DataField, AutoNetworkedField]
    public bool PackLeader;
}

public enum YautjaSelfDestructExplosionType : byte
{
    Big = 0,
    Small = 1,
}

public enum YautjaBracerOwnerRank : byte
{
    Unblooded,
    Elite,
    Elder,
    Leader,
    Admin,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaPowerSystem), typeof(YautjaMaskSystem), typeof(YautjaCloakSystem), typeof(YautjaSelfDestructSystem), Other = AccessPermissions.ReadWriteExecute)]
public sealed partial class YautjaBracerComponent : Component, IClothingSlots
{
    [DataField]
    public HashSet<EntProtoId>? ActionWhitelist;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxCharge = 3000;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Charge = 3000;

    [DataField]
    public FixedPoint2 Regen = 60;

    [DataField]
    public FixedPoint2 EmpPowerDrain = 1000;

    [DataField]
    public float EmpSeverityOneEnergy = 50000f;

    [DataField, AutoNetworkedField]
    public bool BadBlood;

    [DataField, AutoNetworkedField]
    public YautjaBracerOwnerRank OwnerRank = YautjaBracerOwnerRank.Unblooded;

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
    public EntProtoId OpenBracerMenuActionId = "CMUActionYautjaOpenBracerMenu";

    [ViewVariables]
    public EntityUid? OpenBracerMenuAction;

    [DataField]
    public EntProtoId OpenMarkPanelActionId = "CMUActionYautjaOpenMarkPanel";

    [ViewVariables]
    public EntityUid? OpenMarkPanelAction;

    [DataField]
    public bool EnableRecall = true;

    [DataField]
    public EntProtoId RecallActionId = "CMUActionYautjaRecall";

    [ViewVariables]
    public EntityUid? RecallAction;

    [DataField]
    public EntProtoId CallDiscActionId = "CMUActionYautjaCallDisc";

    [ViewVariables]
    public EntityUid? CallDiscAction;

    [DataField]
    public FixedPoint2 CallDiscPowerCost = 70;

    [DataField]
    public float CallDiscRange = 10f;

    [DataField]
    public float CallDiscActiveRange = 7f;

    [DataField]
    public TimeSpan CallDiscCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan NextCallDisc;

    [DataField]
    public EntProtoId SelfDestructActionId = "CMUActionYautjaSelfDestruct";

    [ViewVariables]
    public EntityUid? SelfDestructAction;

    [DataField]
    public EntProtoId ChangeExplosionTypeActionId = "CMUActionYautjaChangeExplosionType";

    [ViewVariables]
    public EntityUid? ChangeExplosionTypeAction;

    [DataField]
    public EntProtoId ToggleLockActionId = "CMUActionYautjaToggleBracerLock";

    [ViewVariables]
    public EntityUid? ToggleLockAction;

    [DataField]
    public EntProtoId TranslatorActionId = "CMUActionYautjaTranslator";

    [ViewVariables]
    public EntityUid? TranslatorAction;

    [DataField]
    public EntProtoId ToggleIdChipActionId = "CMUActionYautjaToggleBracerIdChip";

    [ViewVariables]
    public EntityUid? ToggleIdChipAction;

    [DataField]
    public EntProtoId ToggleNotificationSoundActionId = "CMUActionYautjaToggleBracerNotificationSound";

    [ViewVariables]
    public EntityUid? ToggleNotificationSoundAction;

    [DataField]
    public EntProtoId ToggleBracerNameActionId = "CMUActionYautjaToggleBracerName";

    [ViewVariables]
    public EntityUid? ToggleBracerNameAction;

    [DataField]
    public EntProtoId TrackGearActionId = "CMUActionYautjaTrackGear";

    [ViewVariables]
    public EntityUid? TrackGearAction;

    [DataField]
    public EntProtoId AddTrackedItemActionId = "CMUActionYautjaAddTrackedItem";

    [ViewVariables]
    public EntityUid? AddTrackedItemAction;

    [DataField]
    public EntProtoId RemoveTrackedItemActionId = "CMUActionYautjaRemoveTrackedItem";

    [ViewVariables]
    public EntityUid? RemoveTrackedItemAction;

    [DataField]
    public EntProtoId CreateStabilisingCrystalActionId = "CMUActionYautjaCreateStabilisingCrystal";

    [ViewVariables]
    public EntityUid? CreateStabilisingCrystalAction;

    [DataField]
    public EntProtoId CreateHealingCapsuleActionId = "CMUActionYautjaCreateHealingCapsule";

    [ViewVariables]
    public EntityUid? CreateHealingCapsuleAction;

    [DataField]
    public EntProtoId CreateHumanStabilisingCrystalActionId = "CMUActionYautjaCreateHumanStabilisingCrystal";

    [ViewVariables]
    public EntityUid? CreateHumanStabilisingCrystalAction;

    [DataField]
    public EntProtoId CreateHuntingTrapActionId = "CMUActionYautjaCreateHuntingTrap";

    [ViewVariables]
    public EntityUid? CreateHuntingTrapAction;

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
    public string IdCardContainerId = "cmu-yautja-id-card";

    [DataField]
    public EntProtoId IdChipPrototype = "CMUYautjaBracerIdChip";

    [DataField]
    public EntityUid? IdChip;

    [DataField, AutoNetworkedField]
    public bool IdChipDeployed;

    [DataField, AutoNetworkedField]
    public bool NotificationSound = true;

    [DataField]
    public EntProtoId StabilisingCrystalPrototype = "CMUYautjaHealthShard";

    [DataField]
    public EntProtoId HumanStabilisingCrystalPrototype = "CMUYautjaHumanStabilisingCrystal";

    [DataField]
    public EntProtoId HuntingTrapPrototype = "CMUYautjaHuntingTrap";

    [DataField]
    public FixedPoint2 StabilisingCrystalCost = 400;

    [DataField]
    public FixedPoint2 HumanStabilisingCrystalCost = 400;

    [DataField]
    public FixedPoint2 HealingCapsuleCost = 600;

    [DataField]
    public EntProtoId HealingCapsulePrototype = "CMUYautjaHealingCapsule";

    [DataField]
    public TimeSpan HealingCapsuleCooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public FixedPoint2 HuntingTrapCost = 300;

    [DataField]
    public TimeSpan StabilisingCrystalCooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan HuntingTrapCooldown = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan NextStabilisingCrystal;

    [DataField]
    public TimeSpan NextHealingCapsule;

    [DataField]
    public TimeSpan NextHuntingTrap;

    [DataField, AutoNetworkedField]
    public bool SelfDestructArmed;

    [DataField, AutoNetworkedField]
    public YautjaSelfDestructExplosionType SelfDestructExplosionType = YautjaSelfDestructExplosionType.Small;

    [DataField, AutoNetworkedField]
    public TimeSpan SelfDestructAt;

    [DataField]
    public TimeSpan NextSelfDestructWarning;

    [DataField]
    public TimeSpan SelfDestructDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public bool AutoSelfDestructOnUserDeath;

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
    public SoundSpecifier EquipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public SoundSpecifier CloakOnSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_cloakon_modern.ogg");

    [DataField]
    public SoundSpecifier CloakOffSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_cloakoff_modern.ogg");

    [DataField]
    public YautjaInvisibilitySound InvisibilitySound = YautjaInvisibilitySound.Modern;

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
    public TimeSpan CloakDuration = TimeSpan.Zero;

    [DataField]
    public TimeSpan CloakCooldown = TimeSpan.Zero;

    [DataField]
    public TimeSpan CloakWarningBefore = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier? CloakWarningSound;

    [DataField, AutoNetworkedField]
    public TimeSpan CloakExpiresAt;

    [DataField, AutoNetworkedField]
    public TimeSpan CloakCooldownUntil;

    [ViewVariables]
    public bool CloakWarningPlayed;

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
    public SoundSpecifier RecallSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier SelfDestructArmSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_countdown.ogg", AudioParams.Default.WithVolume(8f).WithMaxDistance(40f));

    [DataField]
    public SoundSpecifier SelfDestructCancelSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_off.wav");

    [DataField]
    public SoundSpecifier SelfDestructWarningSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav", AudioParams.Default.WithVolume(6f).WithMaxDistance(35f));

    [DataField]
    public SoundSpecifier SelfDestructLaughSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Voice/Death/pred_deathlaugh.wav", AudioParams.Default.WithVolume(8f).WithMaxDistance(40f));

    [DataField]
    public SoundSpecifier OverloadDoAfterSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/self_destruct_doafter.wav");

    [DataField]
    public TimeSpan OverloadDoAfterDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan OverloadDetonationDelay = TimeSpan.FromSeconds(8);

    [ViewVariables]
    public EntityUid? SelfDestructArmStream;

    [ViewVariables]
    public EntityUid? SelfDestructLaughStream;

    [DataField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_RMC14/Medical/air_release.ogg", AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier MessageSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public SoundSpecifier TranslatorSound = new SoundCollectionSpecifier("CMUYautjaTranslator");

    [DataField]
    public YautjaTranslatorType TranslatorType = YautjaTranslatorType.Modern;

    [DataField]
    public FixedPoint2 TranslatorCost = 50;

    [DataField]
    public SoundSpecifier IdChipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier FabricateSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public DamageSpecifier TechShockDamage = new()
    {
        DamageDict = new()
        {
            { "Heat", 10 },
        },
    };

    [DataField]
    public TimeSpan TechShockStun = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier TechShockSound = new SoundPathSpecifier("/Audio/Effects/sparks2.ogg", AudioParams.Default.WithVolume(8f));

    [DataField]
    public SoundSpecifier TechDelimbSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/WristBlades/wristblades_on.wav", AudioParams.Default.WithVolume(-8f));

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
    public float NonYautjaCloakShockChance = 0.04f;

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
    public bool RequiresYautjaWearer;

    [DataField]
    public float ZoomLevel = 0.45f;

    [DataField]
    public float ZoomOffset = 14f;

    [DataField]
    public EntityUid? User;

    [DataField]
    public EntProtoId VisorGlassesPrototype = "CMUYautjaNightVisionGlasses";

    [DataField]
    public EntityUid? VisorGlasses;

    [DataField]
    public bool PreserveVisorOnUnequip;

    [DataField]
    public FixedPoint2 Drain = 0;

    [DataField]
    public TimeSpan DrainEvery = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan NextDrain;

    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.MASK;

    [DataField]
    public SoundSpecifier ToggleVisorSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_vision.wav");

    [DataField]
    public SoundSpecifier ZoomOnSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_zoom_on.ogg");

    [DataField]
    public SoundSpecifier ZoomOffSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_zoom_off.ogg");
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

[RegisterComponent]
public sealed partial class YautjaCannonPackComponent : Component
{
    [DataField]
    public EntProtoId UseCannonsActionId = "CMUActionYautjaUsePlasmaCannons";

    [ViewVariables]
    public EntityUid? UseCannonsAction;

    [DataField]
    public EntProtoId CannonPrototype = "CMUYautjaDualPlasmaCannons";

    [DataField]
    public string CannonContainerId = "cmu-yautja-cannon-pack-cannon";

    [ViewVariables]
    public EntityUid? Cannon;

    [ViewVariables]
    public ContainerSlot? CannonContainer;

    [DataField]
    public bool CannonsDeployed;

    [DataField]
    public FixedPoint2 MaxCharge = 2000;

    [DataField]
    public FixedPoint2 Charge = 2000;

    [DataField]
    public FixedPoint2 Regen = 200;

    [DataField]
    public FixedPoint2 DeployCost = 50;

    [DataField]
    public TimeSpan RegenEvery = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextRegen;

    [ViewVariables]
    public EntityUid? User;
}

[RegisterComponent]
public sealed partial class YautjaCannonPackLinkedCannonComponent : Component
{
    [DataField]
    public EntityUid Pack;

    [DataField]
    public EntProtoId Projectile = "CMUYautjaCasterLethalBolt";

    [DataField]
    public FixedPoint2 ChargeCost = 1000;
}

[RegisterComponent]
public sealed partial class YautjaCannonPackProjectileRefundComponent : Component
{
    [DataField]
    public EntityUid Pack;

    [DataField]
    public FixedPoint2 ChargeCost;

    [DataField]
    public bool Fired;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaHudViewerComponent : Component;

[RegisterComponent]
public sealed partial class YautjaMaskVisorGlassesComponent : Component;

[RegisterComponent]
public sealed partial class YautjaCommunicatorComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> RegularChannel = "CMUYautja";

    [DataField]
    public ProtoId<RadioChannelPrototype> BadBloodChannel = "CMUYautjaBadBlood";

    [DataField]
    public ProtoId<RadioChannelPrototype> StrandedChannel = "CMUYautjaStranded";

    [DataField]
    public ProtoId<NpcFactionPrototype> RegularFaction = "CMUYautja";

    [DataField]
    public ProtoId<NpcFactionPrototype> BadBloodFaction = "CMUYautjaBadBlood";

    [DataField]
    public ProtoId<NpcFactionPrototype> StrandedFaction = "CMUYautjaStranded";
}

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
    public EntityUid? BloodedBy;

    [DataField, AutoNetworkedField]
    public string BloodingReason = string.Empty;

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
public sealed partial class YautjaShuttleConsoleComponent : Component;

[RegisterComponent]
public sealed partial class YautjaHivebrokenXenoComponent : Component;

[RegisterComponent]
public sealed partial class YautjaMedicalItemComponent : Component;

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
    public bool TreatsWounds = true;

    [DataField]
    public bool RepairsFractures;

    [DataField]
    public SoundSpecifier? HealSound;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaMarkSystem))]
public sealed partial class YautjaMarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<YautjaMarkKind, EntityUid> Marks = new();

    [DataField, AutoNetworkedField]
    public Dictionary<YautjaMarkKind, string> Reasons = new();
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
    public SoundSpecifier RushSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Predalien/predalien_click.ogg");

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

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaChainedWeaponComponent : Component
{
    [DataField]
    public EntProtoId CallCombiActionId = "CMUActionYautjaCallCombi";

    [DataField]
    public EntityUid? CallCombiAction;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedTo;

    [DataField]
    public FixedPoint2 RecallPowerCost = 70;

    [DataField]
    public bool RequireActive = true;

    [DataField]
    public bool Charged;

    [DataField]
    public float TetherRange = 6f;

    public bool Recalling;
}

[RegisterComponent]
public sealed partial class YautjaPounceBlockComponent : Component
{
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
    public TimeSpan ThrowActivationDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public TimeSpan BoomerangVisualDuration = TimeSpan.FromSeconds(3);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float NonYautjaFiddleChance = 0.75f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HumanDamageMultiplier = 0.7f;

    [DataField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/star_hit.ogg");

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Active;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ActivatingFromThrow;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ReturningToOwner;

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

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? PendingThrowActivator;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PendingThrowActivationAt;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan BoomerangVisualUntil;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaCasterSystem))]
public sealed partial class YautjaCasterComponent : Component
{
    [DataField]
    public FixedPoint2 PowerCost = 100;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_fire.wav");

    [DataField]
    public List<YautjaCasterMode> Modes = new();

    [DataField, AutoNetworkedField]
    public int CurrentMode;

    [DataField, AutoNetworkedField]
    public TimeSpan CooldownUntil;

    [DataField]
    public SoundSpecifier? CooldownSound;
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
    public string ExamineStrength = string.Empty;

    [DataField]
    public float FireRate;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_fire.wav");
}

[RegisterComponent]
public sealed partial class YautjaCasterProjectileRefundComponent : Component
{
    [DataField]
    public EntityUid Bracer;

    [DataField]
    public FixedPoint2 ChargeCost;

    [DataField]
    public bool Fired;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaTechItemComponent : Component
{
    [DataField]
    public float DamageMultiplier = 1.5f;

    [DataField]
    public bool BlockPickup = true;

    [DataField]
    public bool BlockUse = true;

    [DataField]
    public bool AllowNonYautjaActiveHandUse;

    [DataField]
    public bool BlockMelee = true;

    [DataField]
    public bool BlockThrow = true;

    [DataField]
    public bool BlockShoot = true;

    [DataField]
    public LocId ShootDeniedPopup = "cmu-yautja-tech-denied";
}

[RegisterComponent]
public sealed partial class YautjaScalableRepairComponent : Component
{
    [DataField]
    public YautjaScalableRepairStatus Status = YautjaScalableRepairStatus.Damaged;

    [DataField(required: true)]
    public string DamagedText = string.Empty;

    [DataField]
    public string ReinforcedText = "It has been reinforced to be more protective.";
}

[Serializable, NetSerializable]
public enum YautjaScalableRepairStatus : byte
{
    Damaged,
    Reinforced,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaTrackedItemComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaUntrackedItemComponent : Component;

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
    public TimeSpan DeadUseWindow = TimeSpan.FromMinutes(1);

    [DataField]
    public bool RequireTargetActor = true;

    [DataField]
    public bool RequireCritical;

    [DataField]
    public TimeSpan ConsentTimeout = TimeSpan.FromSeconds(10);

    [DataField]
    public string ConsentTitle = "Submit?";

    [DataField]
    public string ConsentMessage = "Do you wish to be Enthralled by the Bad Blood?";

    [DataField]
    public List<ProtoId<JobPrototype>> BannedXenoRoles = new()
    {
        "CMXenoQueen",
        "RMCXenoKing",
        "CMUXenoPredalienLarva",
        "CMXenoDrone",
        "CMXenoCarrier",
        "CMXenoBurrower",
        "CMXenoHivelord",
        "CMUYautjaHellhound",
    };

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public SoundSpecifier FinishSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Voice/Roars/pred_roar1.wav");

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
    public SoundSpecifier PulseSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/signal.ogg");

    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(10);

    [DataField]
    public List<YautjaRelayDestinationKind> AllowedDestinations = new()
    {
        YautjaRelayDestinationKind.YautjaShip,
        YautjaRelayDestinationKind.HumanShip,
        YautjaRelayDestinationKind.Ground,
    };

    [DataField]
    public EntProtoId AddTeleporterLocationActionId = "CMUActionYautjaAddTeleporterLocation";

    [DataField]
    public bool AllowCustomDestinations = true;

    [DataField]
    public EntityUid? AddTeleporterLocationAction;

    [DataField]
    public List<YautjaRelayBeaconCustomDestination> CustomDestinations = new();
}

[DataDefinition]
public sealed partial class YautjaRelayBeaconCustomDestination
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public EntityCoordinates Coordinates;
}

[RegisterComponent]
public sealed partial class YautjaRelayDestinationComponent : Component
{
    [DataField]
    public YautjaRelayDestinationKind Kind = YautjaRelayDestinationKind.YautjaShip;

    [DataField]
    public string Id = string.Empty;

    [DataField]
    public string DisplayName = string.Empty;
}

[RegisterComponent]
public sealed partial class YautjaFalconDroneComponent : Component
{
    [DataField]
    public EntProtoId DeployedPrototype = "CMUYautjaFalconDroneDeployed";

    [DataField]
    public EntProtoId ControlActionId = "CMUActionYautjaFalconControl";

    [ViewVariables]
    public EntityUid? ControlAction;

    [DataField]
    public SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_vision.wav");
}

[RegisterComponent]
public sealed partial class YautjaFalconDroneDeployedComponent : Component
{
    [DataField]
    public string DroneItemContainerId = "cmu-yautja-falcon-drone";

    [DataField]
    public EntityUid? DroneItem;

    [DataField]
    public EntityUid? Controller;

    [DataField]
    public EntityUid? PreviousEyeTarget;

    [DataField]
    public bool ReturnEyeOnDelete = true;

    [DataField]
    public bool ReturnDroneItemOnDelete = true;

    [DataField]
    public EntProtoId DestroyedPrototype = "CMUYautjaFalconDroneDestroyed";

    [DataField]
    public EntProtoId DisabledPrototype = "CMUYautjaFalconDroneDisabled";

    [DataField]
    public bool ConvertingToWreckage;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaFalconHudIconComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<HealthIconPrototype> Icon = "CMUYautjaIconFalconDrone";
}

[RegisterComponent]
public sealed partial class YautjaFalconSourceBracerComponent : Component
{
    [DataField]
    public EntityUid? Controller;
}

[RegisterComponent]
public sealed partial class YautjaFalconControllerComponent : Component
{
    [DataField]
    public EntityUid Drone;

    [DataField]
    public EntityUid? SourceBracer;

    [DataField]
    public EntityUid? PreviousEyeTarget;

    [DataField]
    public EntProtoId RecallActionId = "CMUActionYautjaFalconRecall";

    [ViewVariables]
    public EntityUid? RecallAction;
}

[RegisterComponent]
public sealed partial class YautjaHuntTeleporterComponent : Component
{
    [DataField]
    public YautjaHuntTeleporterKind Kind = YautjaHuntTeleporterKind.Ship;

    [DataField]
    public string? DestinationId;
}

[RegisterComponent]
public sealed partial class YautjaHuntTeleportDestinationComponent : Component
{
    [DataField]
    public YautjaHuntTeleporterKind Kind = YautjaHuntTeleporterKind.Ship;

    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public string DisplayName = string.Empty;
}

[RegisterComponent]
public sealed partial class YautjaHuntConsoleComponent : Component
{
    [DataField]
    public YautjaHuntConsoleKind Kind = YautjaHuntConsoleKind.HuntGround;

    [DataField]
    public string? DestinationId;

    [DataField]
    public List<YautjaHuntingGroundDestination> AvailableDestinations = GetDefaultHuntingGroundDestinations();

    [DataField]
    public EntProtoId HuntPreyPrototype = "CMMobHuman";

    [DataField]
    public EntProtoId BloodingPrototype = "CMUMobYautjaYoungblood";

    [DataField]
    public int SpawnCount = 1;

    [DataField]
    public TimeSpan HuntCooldown = TimeSpan.FromMinutes(20);

    [DataField]
    public TimeSpan BloodingCooldown = TimeSpan.FromMinutes(40);

    [DataField]
    public List<YautjaHuntCallOption> HuntCallOptions = GetDefaultHuntCallOptions();

    [DataField]
    public List<YautjaHuntCallOption> BloodingCallOptions = GetDefaultBloodingCallOptions();

    private static List<YautjaHuntingGroundDestination> GetDefaultHuntingGroundDestinations()
    {
        return new List<YautjaHuntingGroundDestination>
        {
            new()
            {
                Id = "jungle_moon",
                DisplayName = "Jungle Moon",
                MapPath = "/Maps/_CMU14/HuntingGrounds/jungle_moon.yml",
            },
            new()
            {
                Id = "desert_moon",
                DisplayName = "Desert Moon",
                MapPath = "/Maps/_CMU14/HuntingGrounds/desert_moon_caves.yml",
            },
        };
    }

    private static List<YautjaHuntCallOption> GetDefaultHuntCallOptions()
    {
        return new List<YautjaHuntCallOption>
        {
            HuntCall("mixed_small", "Multi Faction (small)", 4, 1.25f, MixedPrey()),
            HuntCall("mixed_group", "Multi Faction (group)", 6, 1.4f, MixedPrey()),
            HuntCall("mixed_large", "Multi Faction (large)", 8, 1.6f, MixedPrey()),
            HuntCall("mixed_larger", "Multi Faction (larger)", 12, 1.8f, MixedPrey()),
            HuntCall("serpents_small", "Serpents (small)", 4, 1f, SerpentPrey()),
            HuntCall("serpents_group", "Serpents (group)", 6, 1.2f, SerpentPrey()),
            HuntCall("serpents_large", "Serpents (large)", 8, 1.4f, SerpentPrey()),
            HuntCall("elite_mixed_small", "Elite Multi Faction (small)", 4, 1.5f, ElitePrey()),
            HuntCall("elite_mixed_group", "Elite Multi Faction (group)", 6, 2f, ElitePrey()),
            HuntCall("elite_mixed_large", "Elite Multi Faction (large)", 8, 2.5f, ElitePrey()),
            HuntCall("elite_mixed_larger", "Elite Multi Faction (larger)", 12, 3f, ElitePrey()),
        };
    }

    private static List<YautjaHuntCallOption> GetDefaultBloodingCallOptions()
    {
        var youngblood = new List<YautjaHuntSpawnEntry>
        {
            Entity("CMUMobYautjaYoungblood"),
        };

        return new List<YautjaHuntCallOption>
        {
            YoungbloodCall("youngblood_solo", "Solo Youngblood (One member)", 1, 1, 0, 0, 5, youngblood),
            YoungbloodCall("youngblood_solo_experienced", "Solo Youngblood (One member - Experienced)", 1, 1, 7, 5, 5, youngblood),
            YoungbloodCall("youngblood_three_inexperienced", "Inexperienced Youngblood Party (Three members)", 2, 3, 2, 0, 5, youngblood),
            YoungbloodCall("youngblood_three_intermediate", "Intermediate Youngblood Party (Three members)", 2, 3, 5, 2, 10, youngblood),
            YoungbloodCall("youngblood_three_experienced", "Experienced Youngblood Party (Three members)", 2, 3, 10, 3, 20, youngblood),
            YoungbloodCall("youngblood_three_mixed", "Mixed experience Youngblood Party (Three members)", 2, 3, 10, 0, 5, youngblood),
            YoungbloodCall("youngblood_pack", "Youngblood Hunting Pack (Six members)", 4, 6, 10, 0, 5, youngblood),
        };
    }

    private static YautjaHuntCallOption HuntCall(
        string id,
        string displayName,
        int spawnCount,
        float cooldownMultiplier,
        List<YautjaHuntSpawnEntry> spawns)
    {
        return new YautjaHuntCallOption
        {
            Id = id,
            DisplayName = displayName,
            SpawnCount = spawnCount,
            CooldownMultiplier = cooldownMultiplier,
            Spawns = spawns,
        };
    }

    private static YautjaHuntCallOption YoungbloodCall(
        string id,
        string displayName,
        int minSpawnCount,
        int spawnCount,
        int maximumYoungbloodHours,
        int rejectionYoungbloodHours,
        int requiredSquadAndXenoHours,
        List<YautjaHuntSpawnEntry> spawns)
    {
        var call = HuntCall(id, displayName, spawnCount, 1f, spawns);
        call.MinSpawnCount = minSpawnCount;
        call.MaximumYoungbloodTime = TimeSpan.FromHours(maximumYoungbloodHours);
        call.RejectionYoungbloodTime = TimeSpan.FromHours(rejectionYoungbloodHours);
        call.RequiredSquadAndXenoTime = TimeSpan.FromHours(requiredSquadAndXenoHours);
        return call;
    }

    private static List<YautjaHuntSpawnEntry> MixedPrey()
    {
        return new List<YautjaHuntSpawnEntry>
        {
            RandomHumanoid("RMCCLFSoldier", 2),
            RandomHumanoid("RMCCLFEngineer"),
            RandomHumanoid("RMCCLFMedic"),
            RandomHumanoid("RMCCLFCellLeader"),
            RandomHumanoid("RMCPMCStandardM63B2"),
            RandomHumanoid("RMCPMCStandardSSG45"),
            RandomHumanoid("RMCPMCStandardM54C2"),
            RandomHumanoid("RMCRoyalMarinesCommando"),
            RandomHumanoid("RMCRoyalMarinesMedic"),
            RandomHumanoid("RMCFreelancerStandard"),
            RandomHumanoid("RMCFreelancerLeader"),
        };
    }

    private static List<YautjaHuntSpawnEntry> SerpentPrey()
    {
        return new List<YautjaHuntSpawnEntry>
        {
            Entity("CMXenoWarrior", 3),
            Entity("CMXenoLurker", 2),
            Entity("CMXenoPraetorian"),
            Entity("CMXenoRavager"),
        };
    }

    private static List<YautjaHuntSpawnEntry> ElitePrey()
    {
        return new List<YautjaHuntSpawnEntry>
        {
            RandomHumanoid("RMCPMCLeader"),
            RandomHumanoid("RMCPMCSniper"),
            RandomHumanoid("RMCPMCEngineer"),
            RandomHumanoid("RMCPMCMedic"),
            RandomHumanoid("RMCRoyalMarinesTeamlead"),
            RandomHumanoid("RMCRoyalMarinesSGO"),
            RandomHumanoid("RMCRoyalMarinesBreacher"),
            RandomHumanoid("RMCRoyalMarinesMarksman"),
        };
    }

    private static YautjaHuntSpawnEntry Entity(EntProtoId prototype, int weight = 1)
    {
        return new YautjaHuntSpawnEntry
        {
            EntityPrototype = prototype,
            Weight = weight,
        };
    }

    private static YautjaHuntSpawnEntry RandomHumanoid(ProtoId<RandomHumanoidSettingsPrototype> settings, int weight = 1)
    {
        return new YautjaHuntSpawnEntry
        {
            RandomHumanoidSettings = settings,
            Weight = weight,
        };
    }
}

[DataDefinition]
public sealed partial class YautjaHuntingGroundDestination
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string DisplayName = string.Empty;

    [DataField(required: true)]
    public string MapPath = string.Empty;
}

[DataDefinition]
public sealed partial class YautjaHuntCallOption
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string DisplayName = string.Empty;

    [DataField]
    public int SpawnCount = 1;

    [DataField]
    public int MinSpawnCount = 1;

    [DataField]
    public TimeSpan MaximumYoungbloodTime;

    [DataField]
    public TimeSpan RejectionYoungbloodTime;

    [DataField]
    public TimeSpan RequiredSquadAndXenoTime;

    [DataField]
    public float CooldownMultiplier = 1f;

    [DataField]
    public List<YautjaHuntSpawnEntry> Spawns = new();
}

[DataDefinition]
public sealed partial class YautjaHuntSpawnEntry
{
    [DataField]
    public EntProtoId? EntityPrototype;

    [DataField]
    public ProtoId<RandomHumanoidSettingsPrototype>? RandomHumanoidSettings;

    [DataField]
    public int Weight = 1;
}

[RegisterComponent]
public sealed partial class YautjaHuntCallComponent : Component
{
    [DataField]
    public YautjaHuntConsoleKind Kind;

    [DataField]
    public EntityUid Requester;

    [DataField]
    public EntityUid? Destination;

    [DataField]
    public string? DestinationId;

    [DataField]
    public string? CallId;

    [DataField]
    public string? CallName;
}

[RegisterComponent]
public sealed partial class YautjaHuntSpawnPointComponent : Component
{
    [DataField]
    public YautjaHuntSpawnKind Kind = YautjaHuntSpawnKind.Prey;

    [DataField]
    public string? DestinationId;
}

[RegisterComponent]
public sealed partial class YautjaHuntEscapeConsoleComponent : Component
{
    [DataField]
    public bool Opened;

    [DataField]
    public TimeSpan MaskScanDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan DialogTimeout = TimeSpan.FromSeconds(15);
}

[RegisterComponent]
public sealed partial class YautjaPreserveShutterComponent : Component;

[RegisterComponent]
public sealed partial class YautjaHuntingGroundComponent : Component;

[RegisterComponent]
public sealed partial class YautjaSleepingHellhoundComponent : Component
{
    [DataField]
    public EntProtoId SpawnPrototype = "CMUMobYautjaHellhound";

    [DataField]
    public SoundSpecifier WakeSound = new SoundPathSpecifier("/Audio/Animals/cat_hiss.ogg");
}

[RegisterComponent]
public sealed partial class YautjaHellhoundComponent : Component
{
    [DataField]
    public EntityUid? YautjaOwner;

    [DataField]
    public float LimbTargetDamageMultiplier = 1.15f;

    [DataField]
    public EntProtoId CameraId = "CMUYautjaHellhoundCamera";
}

[RegisterComponent]
public sealed partial class YautjaHoundWatchingComponent : Component
{
    [DataField]
    public EntityUid Hellhound;

    [DataField]
    public EntityUid? PreviousEyeTarget;
}

[RegisterComponent]
public sealed partial class YautjaHoundWatchedComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Watchers = new();
}

[Serializable, NetSerializable]
public enum YautjaHuntTeleporterKind : byte
{
    Ship,
    Young,
}

[Serializable, NetSerializable]
public enum YautjaRelayDestinationKind : byte
{
    YautjaShip,
    HumanShip,
    Ground,
}

[Serializable, NetSerializable]
public enum YautjaHuntConsoleKind : byte
{
    HuntGround,
    Blooding,
    HuntingGroundSelection,
}

[Serializable, NetSerializable]
public enum YautjaHuntSpawnKind : byte
{
    Prey,
    Youngblood,
}

[RegisterComponent]
public sealed partial class YautjaHoundPadComponent : Component
{
    [DataField]
    public EntProtoId InternalCameraPrototype = "CMUYautjaHoundObservationPadInternalCamera";

    [DataField]
    public EntityUid? InternalCamera;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class YautjaScalpComponent : Component
{
    [DataField, AutoNetworkedField]
    public string TrueDescription = "This is the scalp of an irrelevant human.";

    [DataField, AutoNetworkedField]
    public Color HairColor = Color.White;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaPowerSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class YautjaThrallBracerComponent : Component, IClothingSlots
{
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.GLOVES;

    [DataField]
    public EntityUid? User;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxCharge = 1500;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Charge = 1500;

    [DataField]
    public FixedPoint2 Regen = 60;

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

    [DataField]
    public EntProtoId ToggleNotificationSoundActionId = "CMUActionYautjaToggleBracerNotificationSound";

    [ViewVariables]
    public EntityUid? ToggleNotificationSoundAction;

    [DataField]
    public EntProtoId ToggleLockActionId = "CMUActionYautjaToggleThrallBracerLock";

    [ViewVariables]
    public EntityUid? ToggleLockAction;

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
    public SoundSpecifier EquipSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public SoundSpecifier LinkSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public SoundSpecifier MessageSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField, AutoNetworkedField]
    public bool NotificationSound = true;

    [DataField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");

    [DataField]
    public SoundSpecifier ShockSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg");

    [DataField]
    public SoundSpecifier SelfDestructWarningSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");
}

[RegisterComponent, NetworkedComponent]
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
    public HashSet<YautjaGearKind>? ActionWhitelist;

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
    public EntProtoId RemoveBracerAttachmentsActionId = "CMUActionYautjaRemoveBracerAttachments";

    [ViewVariables]
    public EntityUid? RemoveBracerAttachmentsAction;

    [DataField]
    public Dictionary<YautjaGearKind, EntProtoId> GearPrototypes = GetDefaultGearPrototypes();

    [DataField]
    public FixedPoint2 BracerAttachmentDeployPowerCost = 50;

    [DataField]
    public SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier InstallAttachmentSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier RemoveAttachmentSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg");

    [DataField]
    public SoundSpecifier CasterDeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_on.wav");

    [DataField]
    public SoundSpecifier CasterRetractSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_off.wav");

    [DataField]
    public SoundSpecifier WristBladesDeploySound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/WristBlades/wristblades_on.wav");

    [DataField]
    public SoundSpecifier WristBladesRetractSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/WristBlades/wristblades_off.wav");

    [DataField]
    public Dictionary<YautjaGearKind, EntityUid> Gear = new();

    public Dictionary<YautjaGearKind, EntityUid> SecondaryGear = new();

    public HashSet<EntityUid> InstalledGear = new();

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
public sealed partial class YautjaGearRackComponent : Component
{
    [DataField]
    public YautjaGearRackKind Kind;

    [DataField]
    public string Group = "default";

    [DataField]
    public EntityUid? PrimaryVendor;

    [DataField]
    public int SegmentIndex;

    [DataField]
    public int RunLength = 1;
}

public enum YautjaGearRackKind : byte
{
    Adult,
    Youngblood,
    Thrall,
    BloodedThrall,
    Elder,
    BadBlood,
    Stranded,
}

[RegisterComponent]
public sealed partial class YautjaStoredGearComponent : Component
{
    [DataField]
    public string AttachedContainerId = "cmu-yautja-attached-weapon";

    [DataField]
    public EntityUid? Bracer;

    [DataField]
    public YautjaGearKind Kind;

    [DataField]
    public EntProtoId? DeployedPrototype;

    [DataField]
    public bool Deployed;

    [ViewVariables]
    public EntityUid? AttachmentHolder;

    [ViewVariables]
    public EntityUid? AttachedWeapon;

    public ContainerSlot? AttachedContainer;

    public bool Retracting;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaBracerAttachmentSpeedBonusComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PairedAttackSeconds = 0.6f;
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

[RegisterComponent]
public sealed partial class YautjaCauldronComponent : Component
{
    [DataField]
    public TimeSpan BoilDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public string BaseState = "vat";

    [DataField]
    public string BoilingState = "vat_boiling";
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
public sealed partial class YautjaHonorWorthComponent : Component
{
    [DataField]
    public int LifeKillsTotal;

    [DataField]
    public int DefaultHonorValue = 1;
}

public static class YautjaHonorWorth
{
    public static int Get(EntityUid target, IEntityManager entMan)
    {
        entMan.TryGetComponent(target, out YautjaHonorWorthComponent? worth);
        return Math.Max(worth?.LifeKillsTotal ?? 0, worth?.DefaultHonorValue ?? 1);
    }
}

[RegisterComponent]
public sealed partial class YautjaTrophyDisplayComponent : Component;

[RegisterComponent]
public sealed partial class YautjaPolishingRagComponent : Component
{
    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(5);
}

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
    public SoundSpecifier ClaimSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Voice/Roars/pred_roar1.wav");

    [DataField]
    public SoundSpecifier DuelSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Voice/Roars/pred_roar2.wav");

    [DataField]
    public SoundSpecifier ReleaseSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Voice/Clicks/pred_click01.wav");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField, AutoNetworkedField]
    public EntityUid? TrapOwner;

    [DataField]
    public ProtoId<NpcFactionPrototype> ArmedFaction = "CMUYautja";

    [DataField]
    public TimeSpan ArmDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public EntityUid? TrappedMob;

    [DataField]
    public TimeSpan TrapDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public float TetherRange = 2f;

    [DataField]
    public ProtoId<AlertPrototype>? BreakFreeAlert = "CMUYautjaTrapBreakFree";

    [DataField]
    public TimeSpan BreakFreeDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ReleaseAt;

    [DataField]
    public bool TrappedMobInteractResists;

    [DataField]
    public bool BroadcastOnTrigger = true;

    [DataField]
    public bool CanTriggerYautja;

    [DataField]
    public bool CanConfigureRange = false;

    [DataField]
    public bool ShowRecoverPopup = true;

    [DataField]
    public bool LogTrappedMobFreed;

    [DataField]
    public SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier DisarmSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");

    [DataField]
    public SoundSpecifier TriggerSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    [DataField]
    public LocId DisarmPopup = "cmu-yautja-trap-disarmed";

    [DataField]
    public LocId TriggerPopup = "cmu-yautja-trap-triggered";

    [DataField]
    public bool BlocksXenoHeal;

    [DataField]
    public bool ForceXenoHelpEmote;

    [DataField]
    public bool ForceHumanPainEmote;

    [DataField]
    public TimeSpan XenoInterferenceDuration;
}

[Serializable, NetSerializable]
public sealed record YautjaTrapRangeSelectedEvent(NetEntity User, int Range);

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
public enum YautjaCauldronVisuals : byte
{
    State,
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

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaBowComponent : Component;

[Serializable, NetSerializable]
public enum YautjaBowVisuals : byte
{
    LoadedIcon,
}

[RegisterComponent]
public sealed partial class YautjaShieldBashComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan DazeDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public float ThrowDistance = 1f;

    [DataField]
    public float ThrowSpeed = 10f;

    [ViewVariables]
    public TimeSpan NextBashAt;
}

public enum YautjaSourceShieldType : byte
{
    None,
    Directional,
    DirectionalTwoHands,
}

public enum YautjaSourceShieldChance : byte
{
    None = 0,
    VeryLow = 5,
    Low = 15,
    Medium = 20,
    High = 30,
    VeryHigh = 40,
}

[RegisterComponent]
public sealed partial class YautjaSourceShieldBlockComponent : Component
{
    [DataField]
    public YautjaSourceShieldType ShieldType = YautjaSourceShieldType.Directional;

    [DataField]
    public YautjaSourceShieldChance ReadiedBlock = YautjaSourceShieldChance.VeryHigh;

    [DataField]
    public YautjaSourceShieldChance PassiveBlock = YautjaSourceShieldChance.Medium;

    [DataField]
    public float ProjectileBlockFraction;

    [DataField]
    public bool BlocksOnBack;
}

[RegisterComponent]
public sealed partial class YautjaShieldHeldPrefixComponent : Component
{
    [DataField(required: true)]
    public string Lowered = string.Empty;

    [DataField(required: true)]
    public string Readied = string.Empty;
}

[RegisterComponent]
public sealed partial class YautjaCombistickComponent : Component
{
    [DataField]
    public EntProtoId FoldActionId = "CMUActionYautjaFoldCombistick";

    [DataField]
    public EntityUid? FoldAction;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaChainGauntletComponent : Component
{
    [DataField]
    public EntProtoId GuardActionId = "CMUActionYautjaGuardChainGauntlet";

    [DataField]
    public EntityUid? GuardAction;

    [DataField, AutoNetworkedField]
    public bool GuardActive;

    [DataField, AutoNetworkedField]
    public TimeSpan GuardExpiresAt;

    [DataField, AutoNetworkedField]
    public int PunchKnockback = 4;

    [DataField, AutoNetworkedField]
    public bool HasChain;

    [DataField, AutoNetworkedField]
    public int ComboCounter;

    [DataField, AutoNetworkedField]
    public TimeSpan ComboExpiresAt;

    [DataField]
    public TimeSpan ComboDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public int DisarmFinisherComboRequired = 4;

    [DataField]
    public int HelpFinisherComboRequired = 5;

    [DataField]
    public TimeSpan HelpFinisherKnockdown = TimeSpan.FromSeconds(1);

    [DataField]
    public DamageSpecifier HelpFinisherDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 50 },
        },
    };

    [DataField]
    public int HelpFinisherArmorPiercing = 5;

    [DataField]
    public LocId HelpFinisherMessage = "cmu-yautja-chain-gauntlet-help-message";

    [DataField]
    public SoundSpecifier HelpFinisherSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/ChainGauntlet/hit_punch.wav");

    [DataField]
    public TimeSpan ExecutionDoAfter = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan ExecutionRecovery = TimeSpan.FromSeconds(1.4);

    [DataField]
    public TimeSpan ExecutionLiftDuration = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan ExecutionDropDuration = TimeSpan.FromSeconds(0.4);

    [DataField]
    public float ExecutionLiftHeight = 2f;

    [DataField]
    public DamageSpecifier ExecutionDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 60 },
        },
    };

    [DataField]
    public int ExecutionArmorPiercing = 5;

    [DataField]
    public LocId ExecutionMessage = "cmu-yautja-chain-gauntlet-execution-message";

    [DataField]
    public SoundSpecifier ExecutionTargetSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Melee/bone_break1.wav");

    [DataField]
    public SoundSpecifier ExecutionUserSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Voice/Roars/pred_roar5.wav");

    [DataField]
    public SoundSpecifier ExecutionSlamSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Weapons/Melee/bang.wav");

    [DataField]
    public EntProtoId SlamOverlayPrototype = "RMCEffectSlam";

    [DataField]
    public string SlamOverlayState = "slam";

    [DataField, AutoNetworkedField]
    public bool Executing;

    [DataField, AutoNetworkedField]
    public TimeSpan ExecutionUnlockAt;

    [DataField]
    public float DisarmFinisherThrowSpeed = 10f;

    [DataField]
    public TimeSpan ChainPullDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float ChainPullDistance = 5f;

    [DataField]
    public float ChainPullSpeed = 10f;

    [DataField]
    public string ChainHookBeamPrototype = "CMUYautjaChainGauntletBeam";

    [DataField]
    public string ChainHookBeamState = "chain";

    [DataField]
    public string ChainHookProjectilePrototype = "CMUYautjaChainGauntletHookProjectile";

    [DataField]
    public float ChainHookProjectileMaxRange = 4f;

    [DataField]
    public float ChainHookProjectileSpeed = 10f;

    [DataField]
    public string ChainMessage = "GET OVER HERE!";

    [DataField]
    public string ChainMessageSpeechStyleClass = "yautjaChainSpeech";

    [DataField]
    public float ChainMessageChance = 0.01f;

    [DataField]
    public TimeSpan ForceAirlockDoAfter = TimeSpan.FromSeconds(3);

    [DataField]
    public DamageSpecifier ForceAirlockDamage = new()
    {
        DamageDict = new()
        {
            { "Structural", 100 },
        },
    };

    [DataField]
    public SoundSpecifier ForceAirlockCrashSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/metal_crash.ogg");

    [DataField]
    public TimeSpan ForceResinOpenDoAfter = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan ForceResinCloseDoAfter = TimeSpan.FromSeconds(2);

    [DataField]
    public int GuardPunchKnockback = 7;

    [DataField]
    public int GuardExpiredPunchKnockback = 5;

    [DataField]
    public TimeSpan GuardDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public float GuardSpeedMultiplier = 1.3f;
}

[RegisterComponent]
public sealed partial class YautjaChainGauntletPullComponent : Component
{
    [DataField]
    public EntityUid Puller;

    [DataField]
    public TimeSpan PullAt;

    [DataField]
    public float Distance = 5f;

    [DataField]
    public float Speed = 10f;
}

[RegisterComponent]
public sealed partial class YautjaChainWrapperComponent : Component;

[RegisterComponent]
public sealed partial class YautjaMeleeXenoInterferenceComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);
}

[RegisterComponent]
public sealed partial class YautjaScytheBonusStrikeComponent : Component
{
    [DataField]
    public float Chance = 0.15f;
}

[RegisterComponent]
public sealed partial class YautjaHunterSpearFishingComponent : Component
{
    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(5);

    [DataField]
    public float FailureChance = 0.60f;

    [DataField]
    public int CommonWeight = 60;

    [DataField]
    public int UncommonWeight = 15;

    [DataField]
    public int RareWeight = 5;

    [DataField]
    public int UltraRareWeight = 1;

    [ViewVariables]
    public bool BusyFishing;
}

[RegisterComponent]
public sealed partial class YautjaCeremonialDaggerComponent : Component
{
    [DataField]
    public TimeSpan PrepareDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan FlayDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public DamageSpecifier FirstPassDamage = new()
    {
        DamageDict =
        {
            { "Blunt", 15 },
        },
    };

    [DataField]
    public SoundSpecifier StartFlaySound = new SoundPathSpecifier("/Audio/Weapons/pierce.ogg", AudioParams.Default.WithVolume(-4f));

    [DataField]
    public SoundSpecifier FirstPassSound = new SoundPathSpecifier("/Audio/Weapons/slash.ogg", AudioParams.Default.WithVolume(-4f));
}

[Serializable, NetSerializable]
public enum YautjaFlayingStage : byte
{
    Scalp,
    Strip,
    Skin,
    Complete,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaFlayedComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage;

    [DataField, AutoNetworkedField]
    public YautjaFlayingStage NextStage = YautjaFlayingStage.Scalp;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentFlayer;
}

[RegisterComponent]
public sealed partial class YautjaCleavingGlaiveComponent : Component
{
    [DataField]
    public string SkullContainerId = "cmu-yautja-cleaving-glaive-skull";

    [DataField]
    public bool SkullAttached;

    public ContainerSlot? Container;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaArrowComponent : Component
{
    [DataField, AutoNetworkedField]
    public YautjaArrowWarhead PrimaryWarhead = YautjaArrowWarhead.Standard;

    [DataField, AutoNetworkedField]
    public YautjaArrowWarhead? SecondaryWarhead = YautjaArrowWarhead.Explosive;

    [DataField, AutoNetworkedField]
    public bool Dynamic;

    [DataField, AutoNetworkedField]
    public bool Activated;

    [DataField, AutoNetworkedField]
    public YautjaArrowWarhead SelectedWarhead = YautjaArrowWarhead.Standard;

    [DataField]
    public EntProtoId StandardProjectile = "CMUYautjaArrowProjectile";

    [DataField]
    public EntProtoId ExplosiveProjectile = "CMUYautjaExplosiveArrowProjectile";

    [DataField]
    public EntProtoId EmpProjectile = "CMUYautjaEmpArrowProjectile";

    [DataField]
    public EntProtoId SnareProjectile = "CMUYautjaSnareArrowProjectile";
}

[Serializable, NetSerializable]
public sealed record YautjaArrowWarheadSelectedEvent(NetEntity User, YautjaArrowWarhead Warhead);

[Serializable, NetSerializable]
public enum YautjaArrowWarhead : byte
{
    Standard,
    Explosive,
    Emp,
    Snare,
}

[Serializable, NetSerializable]
public enum YautjaArrowVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum YautjaArrowVisualState : byte
{
    Inert,
    Explosive,
    Emp,
    Dynamic,
    Snare,
}

[RegisterComponent]
public sealed partial class YautjaSnareArrowProjectileComponent : Component
{
    [DataField]
    public EntProtoId SnareArrowPrototype = "CMUYautjaSnareArrow";
}

[RegisterComponent]
public sealed partial class YautjaPlasmaRifleBoltComponent : Component
{
    [DataField]
    public string XenoExtraDamageType = "Heat";

    [DataField]
    public float XenoExtraDamageMultiplier = 0.75f;

    [DataField]
    public TimeSpan XenoInterferenceDuration = TimeSpan.FromSeconds(30);
}

[RegisterComponent]
public sealed partial class YautjaIncendiaryPlasmaProjectileComponent : Component
{
    [DataField]
    public float FireStacks = 20f;

    [DataField]
    public float XenoFireStackMultiplier = 0.5f;

    [DataField]
    public float XenoDamageStackDivisor = 4f;
}

[RegisterComponent]
public sealed partial class YautjaCasterStunProjectileComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan HumanBonusStunTime = TimeSpan.FromSeconds(1);
}

[RegisterComponent]
public sealed partial class YautjaCasterImmobilizerProjectileComponent : Component
{
    [DataField]
    public float StunRange = 7f;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan YautjaStunReduction = TimeSpan.FromSeconds(2);
}

[RegisterComponent]
public sealed partial class YautjaCasterSingleLethalProjectileComponent : Component;

[RegisterComponent]
public sealed partial class YautjaSpikeLauncherComponent : Component;

[RegisterComponent]
public sealed partial class YautjaSpikeLauncherProjectileRefundComponent : Component
{
    [DataField]
    public EntityUid Launcher;

    [DataField]
    public bool Fired;
}

[RegisterComponent]
public sealed partial class YautjaPlasmaWeaponComponent : Component
{
    [DataField]
    public bool ShowFireMode;

    [DataField]
    public string PrimaryFireModeText = "It is set to fire plasma bolts.";

    [DataField]
    public string SecondaryFireModeText = "It is set to fire incendiary plasma bolts.";

    [DataField]
    public string NonYautjaExamineText = string.Empty;

    [DataField]
    public float MinimumShootCharge;

    [DataField]
    public string LowPowerWarning = string.Empty;

    [DataField]
    public float MinimumAmmoCharge;

    [DataField]
    public string MaxChargePopup = string.Empty;

    [DataField]
    public bool RefundUnfiredProjectiles;
}

[RegisterComponent]
public sealed partial class YautjaPlasmaWeaponProjectileRefundComponent : Component
{
    [DataField]
    public EntityUid Weapon;

    [DataField]
    public float ChargeCost;

    [DataField]
    public bool Fired;
}
