using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry;

namespace Content.Shared.EntityConditions.Conditions;

/// <summary>
/// Returns true if this solution entity has an amount of reagent in it within a specified minimum and maximum.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class ReagentEntityConditionSystem : EntityConditionSystem<SolutionComponent, ReagentCondition>
{
    protected override void Condition(Entity<SolutionComponent> entity, ref EntityConditionEvent<ReagentCondition> args)
    {
        args.Result = Check(entity.Comp.Solution, args.Condition);
    }

    /// <summary>
    /// Checks this condition against a solution which may not belong to an entity.
    /// </summary>
    /// <param name="solution">The solution to inspect.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="currentReagent">
    /// The reagent currently causing an effect. Used when the condition does not name one explicitly.
    /// </param>
    public bool Check(
        Solution solution,
        ReagentCondition condition,
        ProtoId<ReagentPrototype>? currentReagent = null)
    {
        var reagent = condition.Reagent ?? currentReagent;
        if (reagent is null)
            return false;

        var quantity = solution.GetTotalPrototypeQuantity(reagent.Value);
        return quantity >= condition.Min && quantity <= condition.Max;
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class ReagentCondition : EntityConditionBase<ReagentCondition>
{
    [DataField]
    public FixedPoint2 Min = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 Max = FixedPoint2.MaxValue;

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        var reagentName = Loc.GetString("entity-condition-guidebook-this-reagent");
        if (Reagent is { } reagent && prototype.Resolve(reagent, out var reagentProto))
            reagentName = reagentProto.LocalizedName;

        return Loc.GetString("entity-condition-guidebook-reagent-threshold",
            ("reagent", reagentName),
            ("max", Max == FixedPoint2.MaxValue ? int.MaxValue : Max.Float()),
            ("min", Min.Float()));
    }
}
