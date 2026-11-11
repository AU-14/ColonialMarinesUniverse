using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

// entity table gate for pred-only loot, passes while a predator hunt is running this round.
// the hunt rule lives on the server, so this just asks whoever answers YautjaHuntActiveQueryEvent
public sealed partial class YautjaHuntActiveCondition : EntityTableCondition
{
    protected override bool EvaluateImplementation(EntityTableSelector root,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var ev = new YautjaHuntActiveQueryEvent();
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return ev.Active;
    }
}

[ByRefEvent]
public record struct YautjaHuntActiveQueryEvent(bool Active = false);
