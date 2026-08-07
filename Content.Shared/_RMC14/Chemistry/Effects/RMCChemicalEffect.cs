using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

/// <summary>
/// Handles every RMC metabolism effect through the current event-based entity-effect API.
/// </summary>
public sealed partial class RMCChemicalEffectSystem : EntityEffectSystem<MetaDataComponent, RMCChemicalEffect>
{
    [Dependency] private SharedEntityEffectsSystem _entityEffects = default!;

    private MetabolismContext? _metabolismContext;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<RMCChemicalEffect> args)
    {
        var context = _metabolismContext;
        var effect = args.Effect;
        effect.Execute(new RMCChemicalEffectArgs(
            entity.Owner,
            EntityManager,
            context?.OrganEntity,
            context?.Source,
            context?.Quantity ?? FixedPoint2.Zero,
            context?.Reagent,
            args.Scale));
    }

    /// <summary>
    /// Applies an RMC effect while making the reagent being metabolized available to its event handler.
    /// Entity-effect events are synchronous, so restoring the previous context also supports nested effects.
    /// </summary>
    public void ApplyMetabolismEffect(
        EntityUid target,
        RMCChemicalEffect effect,
        float scale,
        EntityUid organ,
        Solution source,
        FixedPoint2 quantity,
        ReagentPrototype reagent)
    {
        var previous = _metabolismContext;
        _metabolismContext = new MetabolismContext(organ, source, quantity, reagent);

        try
        {
            _entityEffects.ApplyEffect(target, effect, scale);
        }
        finally
        {
            _metabolismContext = previous;
        }
    }

    private readonly record struct MetabolismContext(
        EntityUid OrganEntity,
        Solution Source,
        FixedPoint2 Quantity,
        ReagentPrototype Reagent);
}

/// <summary>
/// Reagent-specific context that the new generic entity-effect event does not carry itself.
/// </summary>
public readonly record struct RMCChemicalEffectArgs(
    EntityUid TargetEntity,
    IEntityManager EntityManager,
    EntityUid? OrganEntity,
    Solution? Source,
    FixedPoint2 Quantity,
    ReagentPrototype? Reagent,
    float Scale);

public abstract partial class RMCChemicalEffect : EntityEffectBase<RMCChemicalEffect>
{
    [DataField]
    public float Potency;

    private float? _moddedPotency;

    /// <summary>
    ///     The value that should be used in actual calculations for chemical effect.
    ///     Halved since potency is halved before being used.
    /// </summary>
    public float ActualPotency => (_moddedPotency ?? Potency) * 0.5f;

    // Halved again since chemicals tick every second in SS14, not every 2.
    public float PotencyPerSecond => ActualPotency * 0.5f;

    [DataField]
    public float NutFactor;

    [DataField]
    public float NutMetabolism;

    public float NutrimentFactor => NutFactor * NutMetabolism;

    protected abstract override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys);

    public sealed override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return ReagentEffectGuidebookText(prototype, entSys);
    }

    internal void Execute(RMCChemicalEffectArgs args)
    {
        Apply(args);
    }

    protected virtual void Apply(RMCChemicalEffectArgs args)
    {
        if (args.Reagent is not { } reagent)
            return;

        var damageable = args.EntityManager.System<DamageableSystem>();
        var boost = CalculateReagentBoost(args);
        _moddedPotency = Potency + boost;

        try
        {
            var scaledPotency = PotencyPerSecond * args.Scale;
            Tick(damageable, scaledPotency, args);
            var legacyArgs = ToLegacyArgs(args);
            Tick(damageable, scaledPotency, legacyArgs);

            var totalQuantity = FixedPoint2.Zero;
            if (args.Source != null)
                totalQuantity = args.Source.GetTotalPrototypeQuantity(reagent.ID);

            if (reagent.Overdose != null && totalQuantity >= reagent.Overdose)
            {
                TickOverdose(damageable, scaledPotency, args);
                TickOverdose(damageable, scaledPotency, legacyArgs);
            }

            if (reagent.CriticalOverdose != null && totalQuantity >= reagent.CriticalOverdose)
            {
                TickCriticalOverdose(damageable, scaledPotency, args);
                TickCriticalOverdose(damageable, scaledPotency, legacyArgs);
            }
        }
        finally
        {
            _moddedPotency = null;
        }
    }

    private static EntityEffectReagentArgs ToLegacyArgs(RMCChemicalEffectArgs args)
        => new(args.TargetEntity,
            args.EntityManager,
            args.OrganEntity,
            args.Source,
            args.Quantity,
            args.Reagent,
            null,
            args.Scale);

    private static float CalculateReagentBoost(RMCChemicalEffectArgs args)
    {
        var boost = 0f;
        if (args.Reagent?.Metabolisms is not { } metabolisms)
            return boost;

        foreach (var entry in metabolisms.Metabolisms.Values)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is RMCChemicalEffect rmcEffect)
                    rmcEffect.ReagentBoost(args, ref boost);
            }
        }

        return boost;
    }

    protected virtual void ReagentBoost(RMCChemicalEffectArgs args, ref float boost)
    {
    }

    protected virtual void ReagentBoost(EntityEffectReagentArgs args, ref float boost)
    {
    }

    protected virtual void Tick(DamageableSystem damageable, FixedPoint2 potency, RMCChemicalEffectArgs args)
    {
    }

    protected virtual void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, RMCChemicalEffectArgs args)
    {
    }

    protected virtual void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, RMCChemicalEffectArgs args)
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
