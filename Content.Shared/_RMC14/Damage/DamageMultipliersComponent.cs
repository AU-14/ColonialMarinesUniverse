using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Damage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCDamageableSystem))]
public sealed partial class DamageMultipliersComponent : Component
{
    // Runtime-added projectile components need a collection before their first network state is applied.
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<DamageMultiplierFlag, float> Multipliers = new();
}
