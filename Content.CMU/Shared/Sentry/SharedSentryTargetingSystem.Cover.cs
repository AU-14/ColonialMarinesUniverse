using Content.Shared.BarricadeBlock;

namespace Content.Shared._RMC14.Sentry;

public abstract partial class SharedSentryTargetingSystem
{
    private const float SentryCoverDistance = 4f;

    public bool CanTargetAcrossBarricade(EntityUid shooter, EntityUid blocker)
    {
        return TryComp<BarricadeBlockComponent>(blocker, out var cover)
               && (cover.Blocking < 100 || ClearsBarricade(shooter, blocker, cover));
    }

    public bool ClearsBarricade(EntityUid shooter, EntityUid blocker, BarricadeBlockComponent cover)
    {
        var distance = HasComp<SentryTargetingComponent>(shooter) ? SentryCoverDistance : cover.Distance;
        var origin = Transform(shooter).Coordinates;
        if (!origin.TryDistance(EntityManager, _xform, Transform(blocker).Coordinates, out var separation)
            || separation > distance)
            return false;

        var facing = _xform.GetWorldRotation(shooter).GetCardinalDir();
        var barrier = _xform.GetWorldRotation(blocker).GetCardinalDir();
        return facing == barrier || cover.Bidirectional && facing == barrier.GetOpposite();
    }
}
