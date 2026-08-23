using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects;

public sealed class RepairingContactSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> StructuralType = "Structural";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, ReactionEntityEvent>(OnReaction);
    }

    private void OnReaction(Entity<DamageableComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || HasComp<RepairableXenoStructureComponent>(ent))
            return;

        var synth = HasComp<SynthComponent>(ent);
        if (!synth &&
            (ent.Comp.DamageContainerID is not { } containerId ||
             !_prototypes.TryIndex(containerId, out var container) ||
             !container.SupportedTypes.Contains(StructuralType.Id)))
        {
            return;
        }
        if (args.Reagent.Metabolisms == null)
            return;

        Repairing? repairing = null;
        foreach (var metabolism in args.Reagent.Metabolisms.Values)
        {
            foreach (var effect in metabolism.Effects)
            {
                if (effect is Repairing candidate &&
                    (repairing == null || candidate.ActualPotency > repairing.ActualPotency))
                {
                    repairing = candidate;
                }
            }
        }

        if (repairing == null)
            return;

        var amount = (FixedPoint2)(repairing.ActualPotency * 10f) * args.ReagentQuantity.Quantity;
        if (amount <= FixedPoint2.Zero)
            return;

        DamageSpecifier healing;
        if (synth)
        {
            healing = _rmcDamageable.DistributeHealingCached(ent.Owner, BruteGroup, amount);
            healing = _rmcDamageable.DistributeHealingCached(ent.Owner, BurnGroup, amount, healing);
        }
        else
        {
            healing = new DamageSpecifier();
            healing.DamageDict[StructuralType] = -amount;
        }

        _damageable.TryChangeDamage(ent, healing, true, interruptsDoAfters: false);
    }
}
