using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Visor;

/// <summary>
/// Added to a helmet while its squad optic is switched down. Whoever wears the helmet gets the squad optic's effects.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSquadVisorActiveComponent : Component;

/// <summary>
/// On a mob looking through an active squad optic. Shows friendly primed grenades.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUSquadVisorSystem))]
public sealed partial class CMUSquadVisorWearerComponent : Component
{
    /// <summary>The optic gave this mob its squad tracker, so it takes it away again.</summary>
    [DataField, AutoNetworkedField]
    public bool GrantedTracker;
}

/// <summary>
/// A grenade with a smart fuse that a squad optic can pick out. Squad optic wearers see a ring around friendly ones
/// once they're primed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSmartGrenadeComponent : Component;
