using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.Gauss;

/// <summary>
///     The M60 experimental gauss gun. It must be powered on (an <c>ItemToggle</c>) before it fires.
///     The trigger fires a quick hitscan bolt off the gun's battery; the unique action spins the gun up
///     for a charged shot that is released with the trigger once fully charged. A charge held too long
///     overcharges and discharges on its own, with a chance to burn or gib the person holding it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUGaussGunComponent : Component
{
    /// <summary>When the current charge started, or null when the gun is not charging.</summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan? ChargeStart;

    /// <summary>Who is charging the gun.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ChargeUser;

    /// <summary>Seconds of charging before the charged shot can be released.</summary>
    [DataField]
    public TimeSpan ChargeTime = TimeSpan.FromSeconds(4);

    /// <summary>Seconds after the charge started at which the gun discharges by itself.</summary>
    [DataField]
    public TimeSpan OverchargeTime = TimeSpan.FromSeconds(9);

    /// <summary>How many primary shots worth of battery a charged shot drains.</summary>
    [DataField]
    public int ChargedShotCost = 3;

    [DataField]
    public EntProtoId ChargedShot = "CMUGaussChargedShot";

    [DataField]
    public EntProtoId OverchargedShot = "CMUGaussOverchargedShot";

    // Recoil, as a camera recoil scalar, before the shooter's firearms skill reduces it.
    // The primary shot uses the gun's own RMCSelectiveFire recoil.
    [DataField]
    public float ChargedRecoil = 4f;

    [DataField]
    public float OverchargedRecoil = 7f;

    /// <summary>Fraction of recoil removed per firearms skill level.</summary>
    [DataField]
    public float RecoilReductionPerSkill = 0.2f;

    /// <summary>The least recoil a skilled shooter can bring it down to, as a fraction.</summary>
    [DataField]
    public float MinRecoilFraction = 0.3f;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> RecoilSkill = "RMCSkillFirearms";

    // Overcharge mishaps, rolled on the shooter when the gun discharges by itself.
    [DataField]
    public float SelfBurnChance = 0.2f;

    [DataField]
    public DamageSpecifier SelfBurnDamage = new() { DamageDict = { ["Heat"] = 25 } };

    /// <summary>Chance an overcharge vaporizes its holder. See <see cref="CMUGaussVaporizingComponent"/>.</summary>
    [DataField]
    public float GibChance = 0.03f;

    // Hacking the ID lock off with a multitool.
    [DataField]
    public TimeSpan HackTime = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan HackReductionPerSkill = TimeSpan.FromSeconds(20);

    [DataField]
    public EntProtoId<SkillDefinitionComponent> HackSkill = "RMCSkillEngineer";

    [DataField]
    public EntProtoId ToggleAction = "CMUActionToggleGaussGun";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>The shot itself, for both a released charge and an overcharge.</summary>
    [DataField]
    public SoundSpecifier ChargedFireSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/m60gauss/tau_gaus.ogg");

    [DataField]
    public SoundSpecifier SpinupSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/m60gauss/tau_gaus_spinup.ogg");

    [DataField]
    public SoundSpecifier LoopSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/m60gauss/tau_gauss_loop.ogg");

    /// <summary>Plays on top of the loop once the shot is fully charged.</summary>
    [DataField]
    public SoundSpecifier OverchargingSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/m60gauss/tau_gauss_overcharging.ogg");

    /// <summary>Plays when the gun discharges by itself.</summary>
    [DataField]
    public SoundSpecifier OverchargedSound = new SoundPathSpecifier("/Audio/CMU14/Weapons/m60gauss/tau_guass_overcharged.ogg");


    // Server-side charge bookkeeping.
    [ViewVariables]
    public bool FullyCharged;

    [ViewVariables]
    public TimeSpan? LoopStartAt;

    [ViewVariables]
    public EntityUid? SpinupStream;

    [ViewVariables]
    public EntityUid? LoopStream;

    [ViewVariables]
    public EntityUid? OverchargingStream;

    /// <summary>
    ///     Where the charging player's crosshair last was, reported by their client while the gun charges.
    ///     An overcharge discharges toward it.
    /// </summary>
    [ViewVariables]
    public MapCoordinates? AimTarget;

    /// <summary>
    ///     Where the shot being fired right now was aimed, set on the server just before it's traced. Lets
    ///     the beam hit someone lying down that the crosshair was on.
    /// </summary>
    [ViewVariables]
    public MapCoordinates? ShotAim;
}

/// <summary>
///     Sent by the client while it is charging a gauss gun, so an overcharge can fire at the crosshair.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUGaussAimMessage(NetEntity gun, MapCoordinates target) : EntityEventArgs
{
    public readonly NetEntity Gun = gun;
    public readonly MapCoordinates Target = target;
}

public sealed partial class CMUGaussToggleActionEvent : InstantActionEvent;

/// <summary>Multitool do-after that strips the ID lock off the gun.</summary>
[Serializable, NetSerializable]
public sealed partial class CMUGaussHackDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Raised on the gun on the server when the trigger is pulled on a fully charged shot.</summary>
[ByRefEvent]
public readonly record struct CMUGaussReleaseChargeEvent(EntityUid User, EntityCoordinates Target);
