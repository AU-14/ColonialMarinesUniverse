using Content.Shared.Atmos;
using Content.Shared.Body.Components;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Adjust the amount of Moles stored in this set of lungs based on a given dictionary of gasses and ratios.
/// The amount of gas adjusted is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ModifyLungGasEntityEffectSystem : EntityEffectSystem<MetaDataComponent, ModifyLungGas>
{
    // TODO: This shouldn't be an entity effect, gasses should just metabolize and make a byproduct by default...
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ModifyLungGas> args)
    {
        var target = args.ReagentContext?.Organ ?? entity.Owner;
        if (!TryComp(target, out LungComponent? lung))
            return;

        var amount = args.ReagentContext is { } context ? (float) context.Quantity.Quantity : args.Scale;

        foreach (var (gas, ratio) in args.Effect.Ratios)
        {
            var quantity = ratio * amount / Atmospherics.BreathMolesToReagentMultiplier;
            if (quantity < 0)
                quantity = Math.Max(quantity, -lung.Air[(int) gas]);
            lung.Air.AdjustMoles(gas, quantity);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ModifyLungGas : EntityEffectBase<ModifyLungGas>
{
    /// <summary>
    /// The new gas composition to set in the lung.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<Gas, float> Ratios = default!;
}
