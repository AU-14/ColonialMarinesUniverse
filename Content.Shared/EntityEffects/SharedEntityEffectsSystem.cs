using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.FixedPoint;
using Content.Shared.Random.Helpers;
using Robust.Shared.Timing;

namespace Content.Shared.EntityEffects;

/// <summary>
/// This handles entity effects.
/// Specifically it handles the receiving of events for causing entity effects, and provides
/// public API for other systems to take advantage of entity effects.
/// </summary>
public sealed partial class SharedEntityEffectsSystem : EntitySystem, IEntityEffectRaiser
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedEntityConditionsSystem _condition = default!;
    [Dependency] private ReagentEntityConditionSystem _reagentCondition = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReactiveComponent, ReactionEntityEvent>(OnReactive);
        SubscribeLocalEvent<EntityEffectOnMapInitComponent, MapInitEvent>(OnMapInit);
    }

    private void OnReactive(Entity<ReactiveComponent> entity, ref ReactionEntityEvent args)
    {
        var before = new BeforeReactionEntityEffectsEvent(args);
        RaiseLocalEvent(entity.Owner, ref before);

        var scale = args.ReagentQuantity.Quantity.Float();
        var reagentContext = new ReagentEffectContext(
            args.Reagent,
            args.Source,
            null,
            null,
            args.ReagentQuantity,
            null,
            args.Method,
            ReagentEffectOrigin.Reaction);

        if (args.Reagent.ReactiveEffects != null && entity.Comp.ReactiveGroups != null)
        {
            foreach (var (key, val) in args.Reagent.ReactiveEffects)
            {
                if (!val.Methods.Contains(args.Method))
                    continue;

                if (!entity.Comp.ReactiveGroups.TryGetValue(key, out var group))
                    continue;

                if (!group.Contains(args.Method))
                    continue;

                ApplyEffects(entity, val.Effects, scale, reagentContext: reagentContext);
            }
        }

        if (entity.Comp.Reactions != null)
        {
            foreach (var entry in entity.Comp.Reactions)
            {
                if (!entry.Methods.Contains(args.Method))
                    continue;

                if (entry.Reagents == null || entry.Reagents.Contains(args.Reagent.ID))
                    ApplyEffects(entity, entry.Effects, scale, reagentContext: reagentContext);
            }
        }
    }

    private void OnMapInit(Entity<EntityEffectOnMapInitComponent> entity, ref MapInitEvent args)
    {
        ApplyEffects(entity, entity.Comp.Effects);
    }

    /// <inheritdoc cref="ApplyEffects(EntityUid,EntityEffect[],float,EntityUid?)"/>
    public void ApplyEffects(
        EntityUid target,
        EntityEffect[] effects,
        FixedPoint2 scale,
        EntityUid? user = null,
        ReagentEffectContext? reagentContext = null)
    {
        ApplyEffects(target, effects, scale.Float(), user, reagentContext);
    }

    /// <summary>
    /// Applies a list of entity effects to a target entity. Returns true if at least one succeeded.
    /// </summary>
    /// <param name="target">Entity being targeted by the effects</param>
    /// <param name="effects">Effects we're applying to the entity</param>
    /// <param name="scale">Optional scale multiplier for the effects</param>
    /// <param name="user">The entity causing the effect.</param>
    public bool TryApplyEffects(
        EntityUid target,
        EntityEffect[] effects,
        float scale = 1f,
        EntityUid? user = null,
        ReagentEffectContext? reagentContext = null)
    {
        var success = false;
        // do all effects, if conditions apply
        foreach (var effect in effects)
        {
            success |= TryApplyEffect(target, effect, scale, user, reagentContext);
        }

        return success;
    }

    /// <summary>
    /// Applies a list of entity effects to a target entity. Works using <see cref="TryApplyEffects"/>
    /// </summary>
    /// <param name="target">Entity being targeted by the effects</param>
    /// <param name="effects">Effects we're applying to the entity</param>
    /// <param name="scale">Optional scale multiplier for the effects</param>
    /// <param name="user">The entity causing the effect.</param>
    public void ApplyEffects(
        EntityUid target,
        EntityEffect[] effects,
        float scale = 1f,
        EntityUid? user = null,
        ReagentEffectContext? reagentContext = null)
    {
        TryApplyEffects(target, effects, scale, user, reagentContext);
    }

    /// <summary>
    /// Applies an entity effect to a target if all conditions pass.
    /// </summary>
    /// <param name="target">Target we're applying an effect to</param>
    /// <param name="effect">Effect we're applying</param>
    /// <param name="scale">Optional scale multiplier for the effect.</param>
    /// <param name="user">The entity causing the effect.</param>
    /// <returns>True if all conditions pass!</returns>
    public bool TryApplyEffect(
        EntityUid target,
        EntityEffect effect,
        float scale = 1f,
        EntityUid? user = null,
        ReagentEffectContext? reagentContext = null)
    {
        if (scale < effect.MinScale)
            return false;

        // TODO: Replace with proper random prediciton when it exists.
        if (effect.Probability <= 1f && !SharedRandomExtensions.PredictedProb(_timing, effect.Probability, GetNetEntity(target), GetNetEntity(user)))
                return false;

        // See if conditions apply
        if (!TryConditions(target, effect.Conditions, reagentContext))
            return false;

        ApplyEffect(target, effect, scale, user, reagentContext);
        return true;
    }

    private bool TryConditions(
        EntityUid target,
        EntityCondition[]? conditions,
        ReagentEffectContext? reagentContext)
    {
        if (conditions is null)
            return true;

        foreach (var condition in conditions)
        {
            if (condition is ReagentCondition reagentCondition)
            {
                var result = reagentContext is { Source: { } source } context &&
                             _reagentCondition.Check(
                                 source,
                                 reagentCondition,
                                 context.Quantity.Reagent.Prototype);
                if (reagentCondition.Inverted == result)
                    return false;

                continue;
            }

            if (!_condition.TryCondition(target, condition))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Applies an <see cref="EntityEffect"/> to a given target.
    /// This doesn't check conditions so you should only call this if you know what you're doing!
    /// </summary>
    /// <param name="target">Target we're applying an effect to</param>
    /// <param name="effect">Effect we're applying</param>
    /// <param name="scale">Optional scale multiplier for the effect.</param>
    /// <param name="user">The entity causing the effect.</param>
    public void ApplyEffect(
        EntityUid target,
        EntityEffect effect,
        float scale = 1f,
        EntityUid? user = null,
        ReagentEffectContext? reagentContext = null)
    {
        // Clamp the scale if the effect doesn't allow scaling.
        if (!effect.Scaling)
            scale = Math.Min(scale, 1f);

        if (effect.Impact is { } level)
        {
            var chemistry = reagentContext is { } context
                ? $" from reagent {context.Reagent.ID}"
                  + (context.Method is { } method ? $" via {method}" : string.Empty)
                : string.Empty;
            var effectName = effect.GetType().Name;
            var coordinates = Transform(target).Coordinates;
            var logType = reagentContext is null ? effect.LogType : LogType.EntityEffect;
            _adminLog.Add(
                logType,
                level,
                $"Entity effect {effectName:effect} applied on entity {target:entity} at {coordinates:coordinates} with a scale multiplier of {scale}{chemistry}"
            );
        }

        effect.RaiseEvent(target, this, scale, user, reagentContext);
    }

    /// <summary>
    /// Raises an effect to an entity. You should not be calling this unless you know what you're doing.
    /// </summary>
    public void RaiseEffectEvent<T>(
        EntityUid target,
        T effect,
        float scale,
        EntityUid? user,
        ReagentEffectContext? reagentContext = null) where T : EntityEffectBase<T>
    {
        var effectEv = new EntityEffectEvent<T>(effect, scale, user, reagentContext);
        RaiseLocalEvent(target, ref effectEv);
    }
}

/// <summary>
/// This is a basic abstract entity effect containing all the data an entity effect needs to affect entities with effects...
/// </summary>
/// <typeparam name="T">The Component that is required for the effect</typeparam>
/// <typeparam name="TEffect">The Entity Effect itself</typeparam>
public abstract partial class EntityEffectSystem<T, TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase<TEffect>
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(Effect);
    }

    protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args);
}

/// <summary>
/// Used to raise an EntityEffect without losing the type of effect.
/// </summary>
public interface IEntityEffectRaiser
{
    void RaiseEffectEvent<T>(
        EntityUid target,
        T effect,
        float scale,
        EntityUid? user,
        ReagentEffectContext? reagentContext = null) where T : EntityEffectBase<T>;
}

[ByRefEvent]
public readonly record struct BeforeReactionEntityEffectsEvent(ReactionEntityEvent Reaction);
