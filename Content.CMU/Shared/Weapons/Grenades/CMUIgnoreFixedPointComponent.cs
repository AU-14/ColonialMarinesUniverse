using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Grenades;

/// <summary>
/// Projectiles fired from a grenade launcher are normally lobbed: they arc over mobs and stop at the clicked point.
/// Projectiles with this component, like the M108 canister's buckshot, fly straight and hit things instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUIgnoreFixedPointComponent : Component
{
    /// <summary>Speed the projectile is sent off at once it's released from the launcher's arc.</summary>
    [DataField, AutoNetworkedField]
    public float Speed = 62;
}
