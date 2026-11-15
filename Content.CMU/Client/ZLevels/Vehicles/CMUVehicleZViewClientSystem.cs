using Content.Shared.CMU14.ZLevels.Vehicles;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Client.Player;

namespace Content.Client.CMU14.ZLevels.Vehicles;

/// <summary>The hull's viewer is shared by the crew, so each client sets it from its own look-up state.</summary>
public sealed class CMUVehicleZViewClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private CMUVehicleZViewSystem _vehicleView = default!;

    private EntityUid? _hull;

    public override void FrameUpdate(float frameTime)
    {
        var user = _player.LocalEntity;
        var hull = user is { } local && _vehicleView.GetViewSource(local) is var source && source != local
            ? source
            : (EntityUid?) null;

        if (_hull != hull && _hull is { } previous && TryComp<CMUZLevelViewerComponent>(previous, out var stale))
            _zLevels.SetLocalView((previous, stale), false, false);

        _hull = hull;

        if (hull is not { } target ||
            !TryComp<CMUZLevelViewerComponent>(target, out var hullViewer) ||
            !TryComp<CMUZLevelViewerComponent>(user, out var userViewer))
        {
            return;
        }

        var lookUp = userViewer.LookUp && !_zLevels.HasOpaqueAbove(target);
        _zLevels.SetLocalView((target, hullViewer), lookUp, userViewer.FaintUp);
    }
}
