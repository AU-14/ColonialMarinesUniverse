using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityConditions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions;

/// <summary>
/// Checks a solution for a reagent quantity. During metabolism a null reagent means the reagent currently being metabolized.
/// </summary>
public sealed partial class ReagentThresholdEntityConditionSystem : EntityConditionSystem<SolutionComponent, ReagentThreshold>
{
    protected override void Condition(Entity<SolutionComponent> entity, ref EntityConditionEvent<ReagentThreshold> args)
    {
        if (args.Condition.Reagent is not { } reagent)
        {
            args.Result = true;
            return;
        }

        var quantity = entity.Comp.Solution.GetTotalPrototypeQuantity(reagent);
        args.Result = quantity >= args.Condition.Min && quantity <= args.Condition.Max;
    }
}

/// <summary>
/// Compatibility condition for RMC reagent prototypes that use the metabolizing reagent implicitly.
/// </summary>
public sealed partial class ReagentThreshold : EntityConditionBase<ReagentThreshold>
{
    [DataField]
    public FixedPoint2 Min = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 Max = FixedPoint2.MaxValue;

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        ReagentPrototype? reagentProto = null;
        if (Reagent is { } reagent)
            prototype.Resolve(reagent, out reagentProto);

        return Loc.GetString("reagent-effect-condition-guidebook-reagent-threshold",
            ("reagent", reagentProto?.LocalizedName ?? Loc.GetString("reagent-effect-condition-guidebook-this-reagent")),
            ("max", Max == FixedPoint2.MaxValue ? int.MaxValue : Max.Float()),
            ("min", Min.Float()));
    }
}
