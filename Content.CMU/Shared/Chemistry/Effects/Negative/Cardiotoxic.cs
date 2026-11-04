/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

public sealed partial class Cardiotoxic : OrganToxicEffect<HeartComponent>
{
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";
    private static readonly EntProtoId CardiacArrest = "StatusEffectCMUCardiacArrest";

    protected override string OrganName => "heart";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Shock";
    protected override string ExtraText => "Also causes arrhythmia; critical overdoses risk cardiac arrest.";

    protected override void OverdoseExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Arrhythmia, TimeSpan.FromSeconds(3));

    protected override void CriticalExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(0.05f))
            system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, CardiacArrest, TimeSpan.FromSeconds(5));
    }
}
