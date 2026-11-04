/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

public sealed partial class Hepatotoxic : OrganToxicEffect<LiverComponent>
{
    protected override string OrganName => "liver";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Poison";
    protected override string ExtraText => "Also causes nausea.";

    protected override void OverdoseExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, "StatusEffectCMUNausea", TimeSpan.FromSeconds(3));
}
