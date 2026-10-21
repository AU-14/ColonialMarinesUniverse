/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

public sealed partial class Oculotoxic : OrganToxicEffect<EyesComponent>
{
    protected override string OrganName => "eye";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Poison";
    protected override string ExtraText => $"Has a [color=red]{ActualPotency * 10f}%[/color] chance to scar the eyes.";

    protected override void OverdoseExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(MathF.Min(0.5f, args.ActualPotency * 0.1f)))
            system.Blindable.AdjustEyeDamage(args.TargetEntity, 1);
    }
}
