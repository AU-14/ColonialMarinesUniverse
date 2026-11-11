using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.ChemicalIrritants;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

/// <summary>
/// Reagent-driven tear gas. Feeds the chemical-irritant system so a payload beaker of this chemical in a
/// reagent smoke grenade behaves like the hand-authored tear gas canister. Masks and internals are honoured
/// by <c>SharedChemicalIrritantSystem.ApplyIrritantDirect</c>.
/// </summary>
public sealed partial class Irritating : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";

    /// <summary>Irritant parameters; defaults are the tear gas profile.</summary>
    [DataField]
    public ChemicalIrritantProfile Profile = new();

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Applies [color=red]{PotencyPerSecond * 4}[/color] chemical irritant per tick, causing coughing, tearing and eventually blindness. Blocked by gas masks and internals.\n" +
           $"Overdoses have a [color=red]{MathF.Min(25f, ActualPotency * 5f)}%[/color] chance to scar the eyes.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] asphyxiation damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ApplyChemicalIrritant(args.TargetEntity, potency.Float() * 4f, Profile);

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(MathF.Min(0.25f, args.ActualPotency * 0.05f)))
            system.Blindable.AdjustEyeDamage(args.TargetEntity, 1);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[AsphyxiationType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
