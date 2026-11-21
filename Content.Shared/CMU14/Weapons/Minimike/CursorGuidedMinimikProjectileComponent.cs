using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Minimike;

/// <summary>
///     Component for minimike rockets fired by gunship launcher that impact at exact cursor position.
///     The projectile will impact when it reaches the aim point or comes within impact radius.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CursorGuidedMinimikProjectileComponent : Component
{
    /// <summary>
    ///     If true, the projectile impacts at cursor position instead of using normal physics.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CursorGuided = true;

    /// <summary>
    ///     Distance the projectile will travel per update tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TravelSpeed = 20f;

    /// <summary>
    ///     Time before the projectile impacts after being fired.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FlightTimeSeconds = 1.5f;
}
