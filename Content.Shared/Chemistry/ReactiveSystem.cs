using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry;

[UsedImplicitly]
public sealed partial class ReactiveSystem : EntitySystem
{
    // TODO: Someone add documentation, I beg you
    public void DoEntityReaction(EntityUid uid, Solution solution, ReactionMethod method)
    {
        foreach (var reagent in solution.Contents.ToArray())
        {
            ReactionEntity(uid, method, reagent, solution);
        }
    }

    public void ReactionEntity(EntityUid uid, ReactionMethod method, ReagentQuantity reagentQuantity)
    {
        ReactionEntity(uid, method, reagentQuantity, null);
    }

    public void ReactionEntity(
        EntityUid uid,
        ReactionMethod method,
        ReagentQuantity reagentQuantity,
        Solution? source)
    {
        if (reagentQuantity.Quantity == FixedPoint2.Zero)
            return;

        // We throw if the reagent specified doesn't exist.
        if (!ProtoMan.Resolve<ReagentPrototype>(reagentQuantity.Reagent.Prototype, out var proto))
            return;

        ReactionEntity(uid, method, proto, reagentQuantity, source);
    }

    public void ReactionEntity(
        EntityUid uid,
        ReactionMethod method,
        ReagentPrototype proto,
        ReagentQuantity reagentQuantity,
        Solution? source)
    {
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
    Solution? Source = null);
