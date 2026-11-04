using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Throwing;

/// <summary>
///     Items thrown by this entity stun and knock down mobs they hit.
///     Put it on any threat that should weaponize picked-up objects.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUThrownStunSystem))]
public sealed partial class CMUThrownStunComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);
}
