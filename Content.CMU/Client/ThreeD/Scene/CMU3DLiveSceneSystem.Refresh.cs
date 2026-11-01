using Robust.Shared.Profiling;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private IEnumerator<bool>? _refreshSteps;
    private EntityUid _refreshActor;
    private Robust.Shared.Map.MapId _refreshMap;
    private float _refreshRadius;
    private ProfSampler _refreshSlice;
    private int _floorsAdded;

    private bool ShouldYieldRefresh() => _refreshSteps != null && _refreshSlice.Elapsed.TotalMilliseconds >= 6;

    private void CancelRefresh()
    {
        _refreshSteps?.Dispose();
        _refreshSteps = null;
    }

    private void AdvanceRefresh(EntityUid actor, TransformComponent transform)
    {
        if (_refreshSteps != null && (_refreshActor != actor || _refreshMap != transform.MapID || _refreshRadius != ViewRadius))
            CancelRefresh();
        if (_refreshSteps == null && (_untilRefresh <= 0 || _actor != actor || _actorMap != transform.MapID ||
                                     SampleOrigin(_transform.GetWorldPosition(transform)) != _sceneOrigin))
        {
            _refreshActor = actor;
            _refreshMap = transform.MapID;
            _refreshRadius = ViewRadius;
            _refreshSteps = GatherScene().GetEnumerator();
        }
        // Finish an in-flight region while walking; repeatedly cancelling at tile
        // boundaries could prevent a moving player's new snapshot from ever arriving.
        if (_refreshSteps != null)
        {
            _refreshSlice = ProfSampler.StartNew();
            if (!_refreshSteps.MoveNext())
                CancelRefresh();
        }
    }
}
