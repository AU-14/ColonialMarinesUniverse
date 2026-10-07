using Content.Shared._RMC14.Xenonids.Construction.ResinHole;

namespace Content.Server._RMC14.Xenonids.Construction.ResinHole;

public sealed partial class XenoResinHoleSystem
{
    private void OnCMUResinHoleTerminating(Entity<XenoResinHoleComponent> hole, ref EntityTerminatingEvent args)
    {
        // UpdateInRange deliberately skips prone victims, including ones on paused maps.
        // Remove the reference before state serialization can encounter the deleted trap.
        var query = AllEntityQuery<InResinHoleRangeComponent>();
        while (query.MoveNext(out var uid, out var range))
        {
            if (!range.HoleList.Remove(hole.Owner) || TerminatingOrDeleted(uid))
                continue;

            Dirty(uid, range);
            if (range.HoleList.Count == 0)
                RemCompDeferred<InResinHoleRangeComponent>(uid);
        }
    }
}
