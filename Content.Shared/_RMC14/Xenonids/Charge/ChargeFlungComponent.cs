using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Charge;

/// <summary>
///     Added to a mob flung by crusher charge. When this mob collides with
///     another mob during the throw, both take damage and get knocked down.
///     Removed when the throw ends.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoChargeSystem))]
public sealed partial class ChargeFlungComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier CollisionDamage = new()
    {
        DamageDict = { ["Blunt"] = 30 }
    };

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);
}
