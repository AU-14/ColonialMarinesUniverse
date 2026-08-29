using System.Text.Json.Serialization;
using Content.Shared._RMC14.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class EqualHealthChange : EntityEffectBase<EqualHealthChange>
{
    [DataField(required: true)]
    [JsonPropertyName("damage")]
    public List<(ProtoId<DamageGroupPrototype> Group, FixedPoint2 Amount)> Damage = new();

    [DataField]
    [JsonPropertyName("ignoreResistances")]
    public bool IgnoreResistances = true;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var damages = new List<string>();
        var heals = false;
        var deals = false;

        foreach (var (groupId, amount) in Damage)
        {
            if (!prototype.TryIndex(groupId, out var group))
                continue;

            var sign = FixedPoint2.Sign(amount);

            if (sign < 0)
                heals = true;
            if (sign > 0)
                deals = true;

            damages.Add(
                Loc.GetString("health-change-display",
                    ("kind", group.LocalizedName),
                    ("amount", MathF.Abs(amount.Float())),
                    ("deltasign", sign)
                ));
        }

        var healsordeals = heals ? (deals ? "both" : "heals") : (deals ? "deals" : "none");

        return Loc.GetString("entity-effect-guidebook-health-change",
            ("chance", Probability),
            ("changes", ContentLocalizationManager.FormatList(damages)),
            ("healsordeals", healsordeals));
    }

}

public sealed partial class EqualHealthChangeEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, EqualHealthChange>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<EqualHealthChange> args)
    {
        var damage = new DamageSpecifier();
        foreach (var (group, amount) in args.Effect.Damage)
        {
            damage = _rmcDamageable.DistributeDamageCached(entity.Owner, group, amount * args.Scale, damage);
        }

        _damageable.TryChangeDamage(
            entity.Owner,
            damage,
            args.Effect.IgnoreResistances,
            interruptsDoAfters: false);
    }
}
