using System.Linq;
using Content.Shared.Camera;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;

namespace Content.Client.Camera;

public static class CameraMapSelection
{
    public static bool IsRightClickSelectable(CameraMapMarkerUiData marker)
    {
        return marker.Status == CameraMapMarkerStatus.Active;
    }

    public static Color GetMarkerColor(CameraMapMarkerStatus status, bool selected)
    {
        return status switch
        {
            CameraMapMarkerStatus.Invalid => Color.Red,
            CameraMapMarkerStatus.Inactive => Color.Gray,
            _ => selected ? Color.Yellow : Color.Magenta,
        };
    }

    public static NetEntity? SelectGrid(CameraMapUiState state, NetEntity? previous)
    {
        return SelectGrid(state, previous, null);
    }

    public static NetEntity? SelectGrid(
        CameraMapUiState state,
        NetEntity? previous,
        NetEntity? activeCamera)
    {
        if (previous is { } old && state.Grids.Any(grid => grid.Grid == old))
            return old;

        if (activeCamera is { } camera)
        {
            var activeGrid = state.Grids.FirstOrDefault(grid =>
                grid.Markers.Any(marker => marker.Camera == camera));
            if (activeGrid != null)
                return activeGrid.Grid;
        }

        if (state.ConsoleGrid is { } own && state.Grids.Any(grid => grid.Grid == own))
            return own;
        return state.Grids.Count == 0 ? null : state.Grids[0].Grid;
    }
}
