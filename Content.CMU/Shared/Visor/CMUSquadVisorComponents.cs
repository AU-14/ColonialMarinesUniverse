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

    [DataField]
    public EntProtoId IffActionId = "CMUActionSquadVisorIFF";

    [DataField]
    public EntProtoId GrenadeActionId = "CMUActionSquadVisorGrenadeWarnings";

    [DataField, AutoNetworkedField]
    public EntityUid? IffAction;

    [DataField, AutoNetworkedField]
    public EntityUid? GrenadeAction;
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
