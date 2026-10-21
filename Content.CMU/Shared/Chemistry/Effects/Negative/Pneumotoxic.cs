/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

public sealed partial class Pneumotoxic : OrganToxicEffect<LungsComponent>
{
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private static readonly ProtoId<EmotePrototype> CoughEmote = "Cough";

    protected override string OrganName => "lung";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Caustic";
    protected override string ExtraText => "Also causes coughing and asphyxiation.";

    protected override void OverdoseExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[AsphyxiationType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        if (system.Random.Prob(0.1f))
            system.RMCEmote.TryEmoteWithChat(args.TargetEntity, CoughEmote, hideLog: true, ignoreActionBlocker: true, forceEmote: true);
    }
}
