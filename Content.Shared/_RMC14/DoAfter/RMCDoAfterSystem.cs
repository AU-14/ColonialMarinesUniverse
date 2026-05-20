using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.DoAfter;
using Content.Shared.Examine;

namespace Content.Shared._RMC14.DoAfter;

public sealed partial class RMCDoAfterSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;



    public bool ShouldCancel(Shared.DoAfter.DoAfter doAfter)
    {
        // Cancel the DoAfter is the entity is resting.
        if (doAfter.Args.BreakOnRest && HasComp<XenoRestingComponent>(doAfter.Args.User))
            return true;

        if (doAfter.Args.Event is LaserDesignatorDoAfterEvent ev)
        {
            if (doAfter.Args.EventTarget is not { } rangefinder ||
                !TryComp(rangefinder, out RangefinderComponent? rangefinderComp))
            {
                return true;
            }

            var coordinates = GetCoordinates(ev.Coordinates);
            if (!coordinates.IsValid(EntityManager) ||
                !_examine.InRangeUnOccluded(doAfter.Args.User, coordinates, rangefinderComp.Range))
            {
                return true;
            }
        }

        return false;
    }

    public void TryCancel(Entity<DoAfterComponent?> ent, ushort? doAfterIndex)
    {
        if (doAfterIndex == null)
            return;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var doAfters = ent.Comp.DoAfters;
        if (!doAfters.ContainsKey(doAfterIndex.Value))
            return;

        _doAfter.Cancel(ent, doAfterIndex.Value, ent);
    }
}
