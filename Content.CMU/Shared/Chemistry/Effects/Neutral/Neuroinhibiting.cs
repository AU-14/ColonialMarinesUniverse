/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Movement;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Ears;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Neuroinhibiting : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Deals [color=red]{PotencyPerSecond * 0.5f}[/color] eye and ear damage, causes stuttering and a 15% slowdown. Organ damage needs surgery to fully reverse.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond}[/color] brain damage.\n" +
           $"Critical overdoses cause unconsciousness.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        var medical = system.ChemicalMedical;
        medical.DamageOrgan<EyesComponent>(target, potency * 0.5f, ShockType);
        medical.DamageOrgan<EarsComponent>(target, potency * 0.5f, ShockType);
        system.StatusEffects.TryAddStatusEffectDuration(target, StutteringSystem.StutterEffect, TimeSpan.FromSeconds((float)potency * 2f));
        // Not a collection expression: `[..]` into List<T> emits CollectionsMarshal.SetCount (sandbox-forbidden).
        system.TemporarySpeedModifiers.ModifySpeed(target,
            new List<TemporarySpeedModifierSet> { new(TimeSpan.FromSeconds(2), 0.85f, 0.85f) });
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
}
