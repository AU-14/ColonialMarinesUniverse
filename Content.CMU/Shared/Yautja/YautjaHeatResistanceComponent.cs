using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Yautja.HeatResistance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaHeatResistanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FireDamageMultiplier = 0.65f;
}
