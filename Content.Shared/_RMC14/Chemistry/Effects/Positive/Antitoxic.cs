using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Stun;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;

namespace Content.Shared._RMC14.Chemistry.Effects.Positive;

public sealed partial class Antitoxic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageGroupPrototype> ToxinGroup = "Toxin";
    private static readonly ProtoId<DamageGroupPrototype> GeneticGroup = "Genetic";

    private static readonly ProtoId<StatusEffectPrototype> Unconscious = "Unconscious";
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var healing = PotencyPerSecond * 2;
        return $"Heals [color=green]{healing}[/color] toxin damage and removes [color=green]{PotencyPerSecond * 0.5f}[/color] units of toxic chemicals from the bloodstream per second.\n" +
               $"Removes toxins from hydroponic plants.\n" +
               $"Overdoses cause [color=red]{PotencyPerSecond}[/color] damage to the eyes.\n" +
               "Critical overdoses impose at least [color=red]30[/color] seconds of drowsiness and retain a [color=red]5%[/color] chance of unconsciousness.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var rmcDamageable = args.EntityManager.System<SharedRMCDamageableSystem>();
        var healing = rmcDamageable.DistributeHealingCached(args.TargetEntity, ToxinGroup, potency * 2f);

        // TODO RMC14 remove genetic heal once other meds are in for genetic damage
        healing = rmcDamageable.DistributeHealingCached(args.TargetEntity, GeneticGroup, potency * 2f, healing);
        damageable.TryChangeDamage(args.TargetEntity, healing, true, interruptsDoAfters: false);

        var bloodstream = args.EntityManager.System<SharedRMCBloodstreamSystem>();
        bloodstream.RemoveBloodstreamToxins(args.TargetEntity, potency * 0.5f);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<EyesComponent>(args.TargetEntity, potency, "Poison");
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<SharedStatusEffectsSystem>()
            .TryUpdateStatusEffectDuration(args.TargetEntity, Drowsiness, TimeSpan.FromSeconds(30));

        var random = IoCManager.Resolve<IRobustRandom>();
        if (!random.Prob(0.05f))
            return;

        var status = args.EntityManager.System<StatusEffectQuerySystem>();
        status.TryAddStatusEffect<RMCUnconsciousComponent>(
            args.TargetEntity,
            Unconscious,
            TimeSpan.FromSeconds(10),
            true
        );
    }

    protected override void TickHydroTray(DamageableSystem damageable, FixedPoint2 potency, EntityEffectHydroArgs args)
    {
        var ev = new HydroTickEvent<Antitoxic>(potency, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ev);
    }
}
