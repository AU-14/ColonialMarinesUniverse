using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Modifies bleed by a given amount multiplied by scale. This can increase or decrease bleed.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ModifyBleedEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, ModifyBleed>
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<ModifyBleed> args)
    {
        var amount = args.Effect.Amount;
        if (args.ReagentContext is { } context)
        {
            if (args.Effect.Scaled)
                amount *= (float) context.Quantity.Quantity;
            amount *= args.Scale;
        }

        _bloodstream.TryModifyBleedAmount(entity.AsNullable(), amount);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ModifyBleed : EntityEffectBase<ModifyBleed>
{
    [DataField]
    public bool Scaled;

    /// <summary>
    /// Amount of bleed we're applying or removing if negative.
    /// </summary>
    [DataField]
    public float Amount = -1.0f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-modify-bleed-amount", ("chance", Probability), ("deltasign", MathF.Sign(Amount)));
}
