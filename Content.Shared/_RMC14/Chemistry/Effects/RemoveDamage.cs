using System.Text.Json.Serialization;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class RemoveDamage : EntityEffectBase<RemoveDamage>
{
    [DataField(required: true)]
    [JsonPropertyName("group")]
    public ProtoId<DamageGroupPrototype> Group;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        if (!prototype.TryIndex(Group, out var type))
            return null;

        return $"Removes all {type.LocalizedName} damage";
    }

}

public sealed partial class RemoveDamageEntityEffectSystem
    : EntityEffectSystem<DamageableComponent, RemoveDamage>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<RemoveDamage> args)
    {
        if (args.ReagentContext != null && args.Scale < 0.95f)
            return;

        if (!_prototype.TryIndex(args.Effect.Group, out var group))
            return;

        var currentDamage = _damageable.GetAllDamage((entity.Owner, entity.Comp));
        var damage = new DamageSpecifier();
        foreach (var type in group.DamageTypes)
        {
            if (currentDamage.DamageDict.TryGetValue(type, out var amount))
                damage.DamageDict[type] = -amount;
        }

        _damageable.TryChangeDamage(entity, damage, true, interruptsDoAfters: false);
    }
}
