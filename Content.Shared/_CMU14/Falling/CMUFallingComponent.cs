using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Falling;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCMUFallingSystem))]
public sealed partial class CMUFallingComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 Adjust;

    [DataField, AutoNetworkedField]
    public DamageSpecifier? TeleportDamage = default!;
}
