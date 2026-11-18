using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    /// <summary>A live view for synchronous calculations. Use GetAllDamage when a snapshot is needed.</summary>
    internal IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> GetDamageByType(DamageableComponent damageable)
        => damageable.Damage.DamageDict;
}
