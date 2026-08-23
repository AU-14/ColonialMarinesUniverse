using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared._RMC14.Chemistry.Effects;

public abstract partial class RMCChemicalEffect : EntityEffect
{
    [DataField]
    public float Potency;

    private float _moddedPotency;

    /// <summary>
    ///     The value that should be used in actual calculations for chemical effect
    ///     Halved since potency is halved before being used
    /// </summary>
    public float ActualPotency => (_moddedPotency != 0 ? _moddedPotency : Potency) * 0.5f;

    /// <summary>
    ///     Boost-aware property level. Numeric benefits should normally be based on this value so
    ///     level N remains exactly N times the level-one value before a safety cap is applied.
    /// </summary>
    public float LinearLevel => ActualPotency * 2f;

    // Halved again since chemicals tick every second in SS14, not every 2
    public float PotencyPerSecond => ActualPotency * 0.5f;

    [DataField]
    public float NutFactor;
    [DataField]
    public float NutMetabolism;

    public float NutrimentFactor => NutFactor * NutMetabolism;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs { Reagent: { } reagent } reagentArgs && args is not EntityEffectHydroArgs)
        {
            if (args.EntityManager.TryGetComponent<MobStateComponent>(args.TargetEntity, out var mobState))
            {
                var dead = mobState.CurrentState == MobState.Dead;
                if ((dead && !ProcessOnDead) || (!dead && !ProcessOnLiving))
                    return;
            }

            var damageable = args.EntityManager.System<DamageableSystem>();
            var scale = reagentArgs.Scale;
            var boost = CalculateReagentBoost(reagentArgs);
            _moddedPotency = Potency + boost;
            var scaledPotency = PotencyPerSecond * scale;
            Tick(damageable, scaledPotency, reagentArgs);

            var totalQuantity = FixedPoint2.Zero;
            if (reagentArgs.Source != null)
                totalQuantity = reagentArgs.Source.GetTotalPrototypeQuantity(reagent.ID);

            if (reagent.Overdose != null && totalQuantity >= reagent.Overdose)
                TickOverdose(damageable, scaledPotency, reagentArgs);

            if (reagent.CriticalOverdose != null && totalQuantity >= reagent.CriticalOverdose)
                TickCriticalOverdose(damageable, scaledPotency, reagentArgs);
        }
        else if (args is EntityEffectHydroArgs { Reagent: { } hydro } hydroArgs)
        {
            var damageable = args.EntityManager.System<DamageableSystem>();
            var scale = hydroArgs.Scale;
            var boost = CalculateReagentBoost(hydroArgs);
            _moddedPotency = Potency + boost;
            var scaledPotency = PotencyPerSecond * scale;
            TickHydroTray(damageable, scaledPotency, hydroArgs);
        }
        
    }

    private static float CalculateReagentBoost(EntityEffectReagentArgs args)
    {
        var boost = 0f;
        if (args.Reagent?.Metabolisms == null)
            return boost;

        foreach (var (_, entry) in args.Reagent.Metabolisms)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is RMCChemicalEffect rmcEffect)
                {
                    rmcEffect.ReagentBoost(args, ref boost);
                }
            }
        }
        return boost;
    }



    protected virtual void ReagentBoost(EntityEffectReagentArgs args, ref float boost)
    {
    }

    /// <summary>
    ///     Generated reagents that contain a corpse-active property are metabolized as a whole on
    ///     dead mobs. This per-effect guard prevents their unrelated co-properties from also firing.
    /// </summary>
    protected virtual bool ProcessOnDead => false;

    protected virtual bool ProcessOnLiving => true;

    protected virtual void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickHydroTray(DamageableSystem damageable, FixedPoint2 potency, EntityEffectHydroArgs args)
    {
    }
}
[ByRefEvent]
public struct HydroTickEvent<T> where T : RMCChemicalEffect
{
    public FixedPoint2 Potency;
    public EntityEffectHydroArgs Args;
    public HydroTickEvent(FixedPoint2 potency, EntityEffectHydroArgs args)
    {
        Potency = potency;
        Args = args;
    }
}

