using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-702 (D17): the Medic's patching kit, added by the Medic caste. Grants the Field Mend action: a short
/// do-after on a Bad Blood or clan beast in reach that stops surface bleeding, splints the worst fracture, takes the
/// pain down a tier and clears slows (BadBloodMedicSystem). Internal bleeding and organs stay with the medicomp.
/// The Medic's armour counts for <see cref="ArmorMultiplier"/>, and the gear-locked field kit
/// (<see cref="BadBloodMedicKitComponent"/>) goes on at <see cref="KitDelayMultiplier"/> of its time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodFieldMendComponent : Component
{
    [DataField]
    public EntProtoId MendAction = "CMUActionBadBloodFieldMend";

    [DataField, AutoNetworkedField]
    public EntityUid? MendActionEntity;

    [DataField]
    public TimeSpan MendDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Bracer charge spent when a mend finishes.
    /// </summary>
    [DataField]
    public FixedPoint2 MendCost = 150;

    /// <summary>
    /// The same body cannot be mended again (by any Medic) for this long.
    /// </summary>
    [DataField]
    public TimeSpan MendTargetCooldown = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The short pain suppression a mend leaves: one tier for <see cref="MendPainDuration"/>.
    /// </summary>
    [DataField]
    public int MendPainTiers = 1;

    [DataField]
    public float MendPainAccumulationSuppression = 0.25f;

    [DataField]
    public TimeSpan MendPainDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The hidden splint a mend applies through the CMU splint API. The Medic does not carry it.
    /// </summary>
    [DataField]
    public EntProtoId MendSplint = "CMUBadBloodFieldMendSplint";

    [DataField]
    public SoundSpecifier MendSound = new SoundPathSpecifier("/Audio/CMU14/Yautja/Medical/pred_crystal_inject.ogg");

    /// <summary>
    /// The Medic's armour counts for this much (lighter armour is the price of the kit).
    /// </summary>
    [DataField]
    public float ArmorMultiplier = 0.9f;

    /// <summary>
    /// A Medic puts the field kit (hide tourniquet, bone splints) on in this fraction of its usual time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float KitDelayMultiplier = 0.5f;
}

/// <summary>
/// CMU14 ECS-712 (D17): the Medic's saves, added by the Medic caste. Grants Blood Rite (lifts a critical Bad Blood or
/// clan beast out of crit, then rite sickness), the Death's Door passive (critical Bad Bloods near a living Medic lose
/// health more slowly), the stretcher-bearer drag (YautjaPullSpeedSystem) and triage sight (the client overlay).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodBloodRiteComponent : Component
{
    [DataField]
    public EntProtoId RiteAction = "CMUActionBadBloodBloodRite";

    [DataField, AutoNetworkedField]
    public EntityUid? RiteActionEntity;

    [DataField]
    public TimeSpan RiteDelay = TimeSpan.FromSeconds(6);

    /// <summary>
    /// The rite heals the body to this much below its crit threshold.
    /// </summary>
    [DataField]
    public FixedPoint2 RiteMarginBelowCrit = 10;

    [DataField]
    public TimeSpan SecondWindDuration = TimeSpan.FromSeconds(8);

    [DataField]
    public float SecondWindSpeedMultiplier = 1.2f;

    /// <summary>
    /// How long a ritted body must wait before it can be ritted again.
    /// </summary>
    [DataField]
    public TimeSpan RiteSicknessDuration = TimeSpan.FromMinutes(3);

    [DataField]
    public SoundSpecifier RiteSound = new SoundCollectionSpecifier("CMUYautjaSpeechClicks");

    /// <summary>
    /// A green light parented to the ritted body for the second wind (D16: a sound, a text and a visual).
    /// </summary>
    [DataField]
    public EntProtoId RiteGlow = "CMUBadBloodRiteGlow";

    /// <summary>
    /// Death's Door: a critical Bad Blood within this many tiles of a living Medic takes
    /// <see cref="DeathsDoorMultiplier"/> of its asphyxiation and blood-loss damage.
    /// </summary>
    [DataField]
    public float DeathsDoorRange = 5f;

    [DataField]
    public float DeathsDoorMultiplier = 0.5f;

    /// <summary>
    /// A Medic dragging a Bad Blood moves at this speed instead of the clan's drag slowdown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StretcherDragMultiplier = 0.8f;

    /// <summary>
    /// Triage sight: clanmates within this range show their state to the Medic.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TriageRange = 20f;

    /// <summary>
    /// Triage sight shows a clanmate as hurt once their damage reaches this fraction of their crit threshold.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TriageHurtFraction = 0.25f;
}

/// <summary>
/// CMU14 ECS-702: on a body a Medic has Field Mended. It cannot be mended again before <see cref="NextMend"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BadBloodFieldMendedComponent : Component
{
    [DataField, AutoPausedField]
    public TimeSpan NextMend;
}

/// <summary>
/// CMU14 ECS-712: a body just lifted out of crit by a Blood Rite. Faster (<see cref="SpeedMultiplier"/>) and unable to
/// attack until <see cref="ExpiresAt"/>, so the rite buys a retreat, not a second fight.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BadBloodSecondWindComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.2f;
}

/// <summary>
/// CMU14 ECS-712: a body that was ritted. No second Blood Rite until <see cref="ExpiresAt"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BadBloodRiteSicknessComponent : Component
{
    [DataField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

/// <summary>
/// CMU14 ECS-712 (Death's Door): on a critical Bad Blood within reach of a living Medic. Its asphyxiation and
/// blood-loss damage (what drains a critical body: it cannot breathe, it bleeds, it is dragged) is multiplied by
/// <see cref="Multiplier"/>. Kept up to date by BadBloodMedicSystem.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodDeathsDoorComponent : Component
{
    [DataField]
    public float Multiplier = 0.5f;
}

/// <summary>
/// CMU14 ECS-711: the regeneration tonic is in this body. Limbs lost while it lasts regrow after
/// <see cref="Multiplier"/> of the usual delay (BadBloodSurvivalSystem.GetLimbRegrowDelayMultiplier).
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BadBloodTonicRegrowthComponent : Component
{
    [DataField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField]
    public float Multiplier = 0.5f;
}

/// <summary>
/// CMU14 ECS-702: a piece of the Medic's field kit (a hide tourniquet or bone splints). Anyone may use it at the
/// item's own time; a Medic puts it on in <see cref="BadBloodFieldMendComponent.KitDelayMultiplier"/> of that time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodMedicKitComponent : Component;

/// <summary>
/// CMU14 ECS-711: a Lodge cauldron that brews regeneration tonic when a Medic boils a prey limb in it, instead of
/// stripping it to a bone trophy (YautjaMachineSystem).
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodTonicCauldronComponent : Component
{
    [DataField]
    public EntProtoId Output = "CMUBadBloodTonicCrystal";

    [DataField]
    public int Count = 2;
}

public sealed partial class BadBloodFieldMendActionEvent : EntityTargetActionEvent;

public sealed partial class BadBloodBloodRiteActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class BadBloodFieldMendDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BadBloodBloodRiteDoAfterEvent : SimpleDoAfterEvent;
