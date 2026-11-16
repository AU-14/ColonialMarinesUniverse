using Content.Shared.FixedPoint;

// CMU14: lets CMU systems turn an item into retrieve intel at runtime (Bad Blood bracer data-cores and stripped Lodge
// trophies, docs/GraphAudit-BadBlood-ObjectivesPlan.md FLOW-505 and FLOW-507). The intel components only take
// writes from IntelSystem, so this lives in a partial of it.
namespace Content.Shared._RMC14.Intel;

public sealed partial class IntelSystem
{
    /// <summary>
    /// Makes <paramref name="item"/> active retrieve intel worth <paramref name="value"/>: carrying it into a
    /// retrieve-item area credits the points once, the same way a mapped experimental device is credited.
    /// An item that was already credited is left alone.
    /// </summary>
    public void MakeRetrieveIntel(EntityUid item, FixedPoint2 value)
    {
        if (TerminatingOrDeleted(item))
            return;

        var retrieve = EnsureComp<IntelRetrieveItemObjectiveComponent>(item);
        if (retrieve.State == IntelObjectiveState.Complete)
            return;

        retrieve.Value = value;
        retrieve.State = IntelObjectiveState.Active;
        Dirty(item, retrieve);

        // the position check in Update only looks at items carrying this
        EnsureComp<ActiveIntelPositionComponent>(item);
    }
}
