using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Modifies the amount of blood in this entity's bloodstream by a given amount multiplied by scale.
/// This effect can increase or decrease blood level.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ModifyBloodLevelEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, ModifyBloodLevel>
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<ModifyBloodLevel> args)
    {
        var amount = args.Effect.Amount;
        if (args.ReagentContext is { } context)
        {
            if (args.Effect.Scaled)
                amount *= context.Quantity.Quantity;
            amount *= args.Scale;
        }

        _bloodstream.TryModifyBloodLevel(entity.AsNullable(), amount);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ModifyBloodLevel : EntityEffectBase<ModifyBloodLevel>
{
    [DataField]
    public bool Scaled;

    /// <summary>
    /// Amount of bleed we're applying or removing if negative.
    /// </summary>
    [DataField]
    public FixedPoint2 Amount = 1.0f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-modify-blood-level", ("chance", Probability), ("deltasign", MathF.Sign(Amount.Float())));
}
