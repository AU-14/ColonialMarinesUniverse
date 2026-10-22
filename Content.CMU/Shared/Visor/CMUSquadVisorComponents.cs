using Content.Shared._RMC14.MotionDetector;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Visor;

/// <summary>
/// Added to a helmet while its squad optic is switched down. Whoever wears the helmet gets the squad optic's effects.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSquadVisorActiveComponent : Component;

/// <summary>
/// Eyewear that shows the squad optic's ammo HUD while worn, without any of the optic's other effects. Used by the
/// smartgun's head mounted sight, which can't be worn with a squad optic down.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAmmoHudSightComponent : Component;

/// <summary>
/// HUD-only compatibility for GOVFOR weapons with a loaded red-dot indicator, and for all smart weapons.
/// Does not grant the separate in-hand AmmoCounter display.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAmmoHudWeaponComponent : Component
{
    [DataField]
    public bool Enabled = true;
}

/// <summary>
/// A squad optic. It can't be switched down while its wearer holds or wears a smartgun.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSquadOpticComponent : Component;

/// <summary>
/// On a mob looking through an active squad optic. Shows friendly primed grenades and stops the held gun firing into
/// friendlies, each of which the wearer can switch off with an action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUSquadVisorSystem))]
public sealed partial class CMUSquadVisorWearerComponent : Component
{
    /// <summary>The optic gave this mob its squad tracker, so it takes it away again.</summary>
    [DataField, AutoNetworkedField]
    public bool GrantedTracker;

    [DataField, AutoNetworkedField]
    public bool IffEnabled = true;

    [DataField, AutoNetworkedField]
    public bool GrenadeWarningsEnabled = true;

    /// <summary>Whether the optic shows what squadmates' motion trackers pick up.</summary>
    [DataField, AutoNetworkedField]
    public bool MotionLinkEnabled = true;

    /// <summary>Whether the optic shows an eligible held or suit-stored gun's ammo count under the status alerts.</summary>
    [DataField, AutoNetworkedField]
    public bool AmmoHudEnabled = true;

    [DataField]
    public EntProtoId IffActionId = "CMUActionSquadVisorIFF";

    [DataField]
    public EntProtoId GrenadeActionId = "CMUActionSquadVisorGrenadeWarnings";

    [DataField]
    public EntProtoId MotionLinkActionId = "CMUActionSquadVisorMotionLink";

    [DataField]
    public EntProtoId AmmoHudActionId = "CMUActionSquadVisorAmmoHud";

    [DataField, AutoNetworkedField]
    public EntityUid? IffAction;

    [DataField, AutoNetworkedField]
    public EntityUid? GrenadeAction;

    [DataField, AutoNetworkedField]
    public EntityUid? MotionLinkAction;

    [DataField, AutoNetworkedField]
    public EntityUid? AmmoHudAction;
}

/// <summary>
/// On a gun held by someone using squad optic IFF. The gun won't fire with a friendly in the line of fire, at the cost
/// of extra spread and damage falloff.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUSquadVisorSystem))]
public sealed partial class CMUSquadVisorIFFGunComponent : Component
{
    /// <summary>Multiplies the gun's spread.</summary>
    [DataField]
    public float SpreadMultiplier = 1.06f;

    /// <summary>Added to the gun's damage falloff multiplier.</summary>
    [DataField]
    public float FalloffAdd = 0.25f;
}

public sealed partial class CMUToggleSquadVisorIFFActionEvent : InstantActionEvent;

public sealed partial class CMUToggleSquadVisorGrenadeWarningsActionEvent : InstantActionEvent;

public sealed partial class CMUToggleSquadVisorMotionLinkActionEvent : InstantActionEvent;

public sealed partial class CMUToggleSquadVisorAmmoHudActionEvent : InstantActionEvent;

/// <summary>
/// A grenade with a smart fuse that a squad optic can pick out. Squad optic wearers see a ring around friendly ones
/// once they're primed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSmartGrenadeComponent : Component
{
    /// <summary>False on dumb grenades that inherit from a smart one.</summary>
    [DataField]
    public bool Enabled = true;
}

/// <summary>
/// On a squad optic wearer: what the motion trackers of nearby squadmates are picking up, drawn like their own tracker.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSquadMotionLinkComponent : Component, IDetectorComponent
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public List<Blip> Blips { get; set; } = new();

    [DataField, AutoNetworkedField]
    public TimeSpan LastScan { get; set; }

    [DataField, AutoNetworkedField]
    public TimeSpan ScanDuration { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>How close a squadmate with a motion tracker has to be for the optic to link to it.</summary>
    [DataField]
    public float Range = 15f;
}
