using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

/// <summary>
/// Base data for the generated RMC/CMU chemical-property family.
/// Runtime state must stay in <see cref="RMCReagentEffectArgs"/>; these objects are shared prototypes.
/// </summary>
public abstract partial class RMCChemicalEffect : EntityEffectBase<RMCChemicalEffect>
{
    [DataField]
    public float Potency;

    /// <summary>
    /// The unboosted property level used by guidebook text and non-runtime callers.
    /// </summary>
    public float ActualPotency => Potency * 0.5f;

    public float LinearLevel => ActualPotency * 2f;

    // Chemicals tick every second here rather than every two seconds in the source design.
    public float PotencyPerSecond => ActualPotency * 0.5f;

    [DataField]
    public float NutFactor;

    [DataField]
    public float NutMetabolism;

    public float NutrimentFactor => NutFactor * NutMetabolism;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => ReagentEffectGuidebookText(prototype, entSys);

    protected virtual string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;

    protected virtual void ReagentBoost(
        RMCChemicalEffectSystem system,
        RMCReagentEffectArgs args,
        ref float boost)
    {
    }

    /// <summary>
    /// Generated reagents with a corpse-active property are metabolized as a whole on dead mobs.
    /// This per-effect guard prevents unrelated co-properties from firing too.
    /// </summary>
    protected virtual bool ProcessOnDead => false;

    protected virtual bool ProcessOnLiving => true;

    protected virtual void Tick(
        RMCChemicalEffectSystem system,
        DamageableSystem damageable,
        FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
    }

    protected virtual void TickOverdose(
        RMCChemicalEffectSystem system,
        DamageableSystem damageable,
        FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
    }

    protected virtual void TickCriticalOverdose(
        RMCChemicalEffectSystem system,
        DamageableSystem damageable,
        FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
    }

    protected virtual void TickHydroTray(
        RMCChemicalEffectSystem system,
        DamageableSystem damageable,
        FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
    }

    internal void Apply(
        RMCChemicalEffectSystem system,
        DamageableSystem damageable,
        EntityUid target,
        float scale,
        EntityUid? user,
        ReagentEffectContext context)
    {
        var boost = CalculateReagentBoost(system, target, scale, user, context);
        var actualPotency = (Potency + boost) * 0.5f;
        var effectArgs = new RMCReagentEffectArgs(target, scale, user, context, actualPotency);
        var scaledPotency = effectArgs.PotencyPerSecond * scale;

        if (context.Origin == ReagentEffectOrigin.Hydroponics)
        {
            TickHydroTray(system, damageable, scaledPotency, effectArgs);
            return;
        }

        if (system.TryGetMobState(target, out var mobState))
        {
            var dead = mobState.CurrentState == MobState.Dead;
            if ((dead && !ProcessOnDead) || (!dead && !ProcessOnLiving))
                return;
        }

        Tick(system, damageable, scaledPotency, effectArgs);

        var totalQuantity = context.Source?.GetTotalPrototypeQuantity(context.Reagent.ID) ?? FixedPoint2.Zero;
        if (context.Reagent.Overdose != null && totalQuantity >= context.Reagent.Overdose)
            TickOverdose(system, damageable, scaledPotency, effectArgs);

        if (context.Reagent.CriticalOverdose != null && totalQuantity >= context.Reagent.CriticalOverdose)
            TickCriticalOverdose(system, damageable, scaledPotency, effectArgs);
    }

    private float CalculateReagentBoost(
        RMCChemicalEffectSystem system,
        EntityUid target,
        float scale,
        EntityUid? user,
        ReagentEffectContext context)
    {
        var boost = 0f;
        var args = new RMCReagentEffectArgs(target, scale, user, context, ActualPotency);

        if (context.Reagent.Metabolisms == null)
            return boost;

        foreach (var (_, entry) in context.Reagent.Metabolisms.Metabolisms)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is RMCChemicalEffect rmcEffect)
                    rmcEffect.ReagentBoost(system, args, ref boost);
            }
        }

        return boost;
    }
}

/// <summary>
/// Immutable per-application data for an RMC chemical effect.
/// </summary>
public readonly record struct RMCReagentEffectArgs(
    EntityUid TargetEntity,
    float Scale,
    EntityUid? User,
    ReagentEffectContext Context,
    float ActualPotency)
{
    public ReagentPrototype Reagent => Context.Reagent;
    public float LinearLevel => ActualPotency * 2f;
    public float PotencyPerSecond => ActualPotency * 0.5f;
}

/// <summary>
/// Typed hydroponics bridge for the nine chemical properties that affect a plant holder.
/// </summary>
[ByRefEvent]
public readonly record struct HydroTickEvent<T>(
    EntityUid Target,
    FixedPoint2 Potency,
    ReagentQuantity Quantity) where T : RMCChemicalEffect;

/// <summary>
/// Single dispatcher for the complete RMC chemical-property subclass family.
/// </summary>
public sealed partial class RMCChemicalEffectSystem : EntityEffectSystem<MetaDataComponent, RMCChemicalEffect>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<RMCChemicalEffect> args)
    {
        if (args.ReagentContext is not { } context)
            return;

        var effect = args.Effect;
        effect.Apply(this, _damageable, entity, args.Scale, args.User, context);
    }

    internal bool TryGetMobState(EntityUid uid, [NotNullWhen(true)] out MobStateComponent? component)
        => _mobStateQuery.TryComp(uid, out component);
}
