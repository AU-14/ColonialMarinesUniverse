using Content.Shared.Climbing.Components;

namespace Content.Shared.Climbing.Systems;

public sealed partial class ClimbSystem
{
    private readonly Queue<Entity<ClimbingComponent>> _cmuPendingClimbEnds = new();

    private void QueueCMUClimbEnd(EntityUid uid, ClimbingComponent climbing)
    {
        if (climbing.CMUEndClimbPending)
            return;

        climbing.CMUEndClimbPending = true;
        _cmuPendingClimbEnds.Enqueue((uid, climbing));
    }

    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        base.UpdateAfterSolve(prediction, frameTime);
        FlushCMUPendingClimbEnds();
    }

    private void FlushCMUPendingClimbEnds()
    {
        while (_cmuPendingClimbEnds.TryDequeue(out var ent))
        {
            ent.Comp.CMUEndClimbPending = false;
            if (ent.Comp.Deleted || TerminatingOrDeleted(ent) || !ent.Comp.IsClimbing ||
                ent.Comp.NextTransition != null || !_fixturesQuery.TryComp(ent, out var fixtures))
                continue;

            // Another contact may have started later in the same batch, or state
            // application may have moved us onto a different climbable surface.
            var supported = false;
            if (fixtures.Fixtures.TryGetValue(ClimbingFixtureName, out var fixture))
            {
                foreach (var contact in fixture.Contacts.Values)
                {
                    if (contact.IsTouching && contact.OtherFixture(ent).Item2 is { Hard: true } &&
                        _climbableQuery.HasComp(contact.OtherEnt(ent)))
                    {
                        supported = true;
                        break;
                    }
                }
            }

            if (!supported)
                StopClimb(ent, ent.Comp, fixtures);
        }
    }
}
