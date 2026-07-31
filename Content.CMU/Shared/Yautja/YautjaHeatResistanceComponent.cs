using System.Linq;
using Content.Shared.Damage;
using Content.Shared._RMC14.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Yautja.HeatResistance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaHeatResistanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FireDamageMultiplier = 0.65f;

private void OnDamageModifyAfterResist(Entity<YautjaHeatResistanceComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        args.Damage = new DamageSpecifier(args.Damage);
        foreach (var type in args.Damage.DamageDict.Keys.ToArray())
        {
            if (type == "Heat")
                args.Damage.DamageDict[type] *= ent.Comp.FireDamageMultiplier;
        }
    }
}
