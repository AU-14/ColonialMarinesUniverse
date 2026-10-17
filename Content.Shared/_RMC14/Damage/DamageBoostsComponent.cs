using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Damage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCDamageableSystem))]
public sealed partial class DamageBoostsComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public List<DamageBoost> Boosts = new();
}

[DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct DamageBoost(DamageMultiplierFlag Flags, DamageSpecifier Damage);
