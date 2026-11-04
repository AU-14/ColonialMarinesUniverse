using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;

namespace Content.Shared.Chemistry;

[UsedImplicitly]
public sealed partial class ReactiveSystem : EntitySystem
{
    [Dependency] private RMCReagentSystem _reagent = default!;

    /// <summary>
    /// Reacts every reagent in a solution with an entity.
    /// </summary>
    public void DoEntityReaction(EntityUid uid, Solution solution, ReactionMethod method)
    {
        foreach (var reagent in solution.Contents.ToArray())
        {
            ReactionEntity(uid, method, reagent, solution);
        }
    }

    public void ReactionEntity(
        EntityUid uid,
        ReactionMethod method,
        ReagentQuantity reagentQuantity,
        Solution? source = null)
    {
        if (!_reagent.TryIndex(reagentQuantity.Reagent.Prototype, out var proto))
            return;

        ReactionEntity(uid, method, proto, reagentQuantity, source);
    }

    public void ReactionEntity(
        EntityUid uid,
        ReactionMethod method,
        ReagentPrototype proto,
        ReagentQuantity reagentQuantity,
        Solution? source = null)
    {
        if (source is not null)
        {
            reagentQuantity = new ReagentQuantity(
                reagentQuantity.Reagent,
                source.GetReagentQuantity(reagentQuantity.Reagent));
        }

        if (reagentQuantity.Quantity == FixedPoint2.Zero)
            return;

        var ev = new ReactionEntityEvent(method, reagentQuantity, proto, source);
        RaiseLocalEvent(uid, ref ev);
    }
}

public enum ReactionMethod
{
    Touch,
    Injection,
    Ingestion,
}

[ByRefEvent]
public readonly record struct ReactionEntityEvent(
    ReactionMethod Method,
    ReagentQuantity ReagentQuantity,
    ReagentPrototype Reagent,
    Solution? Source);
