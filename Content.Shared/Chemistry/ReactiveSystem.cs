using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry;

[UsedImplicitly]
public sealed partial class ReactiveSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    // TODO: Someone add documentation, I beg you
    public void DoEntityReaction(EntityUid uid, Solution solution, ReactionMethod method)
    {
        foreach (var reagent in solution.Contents.ToArray())
        {
            ReactionEntity(uid, method, reagent);
        }
    }

    public void ReactionEntity(EntityUid uid, ReactionMethod method, ReagentQuantity reagentQuantity)
    {
        // We throw if the reagent specified doesn't exist.
        var proto = _prototypeManager.IndexReagent<ReagentPrototype>(reagentQuantity.Reagent.Prototype);
        ReactionEntity(uid, method, proto, reagentQuantity, source);
    }

    public void ReactionEntity(EntityUid uid, ReactionMethod method, ReagentPrototype proto,
        ReagentQuantity reagentQuantity, Solution? source)
    {
        if (!TryComp(uid, out ReactiveComponent? reactive))
            return;

        // We throw if the reagent specified doesn't exist.
        if (!ProtoMan.Resolve<ReagentPrototype>(reagentQuantity.Reagent.Prototype, out var proto))
            return;

        var ev = new ReactionEntityEvent(method, reagentQuantity, proto);
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
public readonly record struct ReactionEntityEvent(ReactionMethod Method, ReagentQuantity ReagentQuantity, ReagentPrototype Reagent);
