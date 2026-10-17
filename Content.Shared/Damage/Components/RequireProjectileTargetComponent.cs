using Content.Shared.Damage.Systems;
// CMU14: accept acid and anti-vehicle attacks.
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

/// <summary>
/// Prevent the object from getting hit by projetiles unless you target the object.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RequireProjectileTargetSystem))]
public sealed partial class RequireProjectileTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;

    // CMU14: accept acid and anti-vehicle attacks.
    /// <summary>Projectiles that can collide without explicitly selecting this entity.</summary>
    [DataField]
    public EntityWhitelist? AlwaysHitWhitelist;
}
