/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

public sealed partial class Neurotoxic : OrganToxicEffect<CMUBrainComponent>
{
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    protected override string OrganName => "brain";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Shock";
    protected override string ExtraText => "Also causes disorientation; critical overdoses cause unconsciousness.";

    protected override void OverdoseExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Drowsiness, TimeSpan.FromSeconds(3));

    protected override void CriticalExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
}
