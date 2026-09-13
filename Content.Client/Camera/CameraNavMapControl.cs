using Content.Client.Pinpointer.UI;
using Content.Shared.Camera;
using Content.Shared.Pinpointer;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Camera;

public sealed class CameraNavMapControl : NavMapControl
{
    private static readonly ResPath TrianglePath = new("/Textures/Interface/NavMap/beveled_triangle.png");
    private static readonly ResPath CirclePath = new("/Textures/Interface/NavMap/beveled_circle.png");

    private readonly Texture _triangleTexture;
    private readonly Texture _circleTexture;
    private readonly CameraMapGridBinding _gridBinding;
    private readonly Dictionary<NetEntity, string> _markerNames = new();

    private CameraMapUiState? _state;
    private CameraMapGridUiData? _selectedGridData;
    private NetEntity? _activeCamera;
    private NetEntity? _selectedGrid;
    private bool _gridBindingReady;

    public event Action<NetEntity>? CameraSelected;
    public event Action<NetEntity?>? GridChanged;

    internal bool GridBindingReady => _gridBindingReady;

    public CameraNavMapControl()
    {
        _gridBinding = new CameraMapGridBinding(grid => IoCManager.Resolve<IEntityManager>().GetEntity(grid));
        var cache = IoCManager.Resolve<IResourceCache>();
        _triangleTexture = cache.GetResource<TextureResource>(TrianglePath).Texture;
        _circleTexture = cache.GetResource<TextureResource>(CirclePath).Texture;

        TrackedEntitySelectedAction += OnTrackedEntitySelected;
        TrackedEntityRightClickedAction += OnTrackedEntityRightClicked;
    }

    public void SetState(CameraMapUiState state, NetEntity? activeCamera)
    {
        _state = state;
        _activeCamera = activeCamera;

        var selectedGrid = CameraMapSelection.SelectGrid(state, _selectedGrid, activeCamera);
        if (selectedGrid is { } grid)
            SelectGrid(grid);
        else
            ClearSelection();
    }

    public void SelectGrid(NetEntity grid)
    {
        if (_state == null)
            return;

        CameraMapGridUiData? selected = null;
        foreach (var candidate in _state.Grids)
        {
            if (candidate.Grid != grid)
                continue;

            selected = candidate;
            break;
        }

        if (selected == null)
            return;

        var changed = _selectedGrid != grid;
        _selectedGrid = grid;
        _gridBinding.Select(grid);
        _selectedGridData = selected;
        _gridBindingReady = false;

        if (!RefreshSelectedGridBinding())
        {
            MapUid = null;
            TrackedEntities.Clear();
            _markerNames.Clear();
        }

        if (changed)
            GridChanged?.Invoke(grid);
    }

    internal bool RefreshSelectedGridBinding()
    {
        if (_selectedGrid == null || _selectedGridData == null)
            return false;

        if (!_gridBinding.TryResolve(out var grid))
        {
            var hadGrid = MapUid != null;
            MapUid = null;
            _gridBindingReady = false;
            TrackedEntities.Clear();
            _markerNames.Clear();
            if (hadGrid)
                ForceNavMapUpdate();
            return false;
        }

        if (MapUid != grid)
        {
            MapUid = grid;
            _gridBindingReady = false;
        }

        if (_gridBindingReady)
            return true;

        RebuildMarkers(_selectedGridData);
        ForceNavMapUpdate();
        _gridBindingReady = EntManager.HasComponent<NavMapComponent>(grid) &&
                            EntManager.HasComponent<MapGridComponent>(grid) &&
                            EntManager.HasComponent<TransformComponent>(grid);
        return true;
    }

    public bool TryGetMarkerName(NetEntity camera, out string name)
    {
        return _markerNames.TryGetValue(camera, out name!);
    }

    private void ClearSelection()
    {
        var changed = _selectedGrid != null;
        _selectedGrid = null;
        _gridBinding.Clear();
        _selectedGridData = null;
        _gridBindingReady = false;
        MapUid = null;
        TrackedEntities.Clear();
        _markerNames.Clear();
        ForceNavMapUpdate();

        if (changed)
            GridChanged?.Invoke(null);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        RefreshSelectedGridBinding();
        base.FrameUpdate(args);
    }

    private void RebuildMarkers(CameraMapGridUiData grid)
    {
        TrackedEntities.Clear();
        _markerNames.Clear();

        foreach (var marker in grid.Markers)
        {
            _markerNames[marker.Camera] = marker.Name;

            var active = marker.Status == CameraMapMarkerStatus.Active;
            var selected = active && marker.Camera == _activeCamera;
            var texture = active ? _triangleTexture : _circleTexture;
            var color = CameraMapSelection.GetMarkerColor(marker.Status, selected);
            var coordinates = new EntityCoordinates(MapUid!.Value, marker.Position);

            TrackedEntities[marker.Camera] = new NavMapBlip(coordinates, texture, color, false,
                CameraMapSelection.IsRightClickSelectable(marker));
        }
    }

    private void OnTrackedEntitySelected(NetEntity? camera)
    {
        if (camera is not { } selected ||
            !TrackedEntities.TryGetValue(selected, out var blip) ||
            !blip.Selectable)
            return;

        CameraSelected?.Invoke(selected);
    }

    private void OnTrackedEntityRightClicked(NetEntity? camera)
    {
        OnTrackedEntitySelected(camera);
    }
}

internal sealed class CameraMapGridBinding(Func<NetEntity, EntityUid> resolver)
{
    private NetEntity? _selectedGrid;

    public EntityUid? ResolvedGrid { get; private set; }

    public void Select(NetEntity grid)
    {
        if (_selectedGrid == grid)
            return;

        _selectedGrid = grid;
        ResolvedGrid = null;
    }

    public void Clear()
    {
        _selectedGrid = null;
        ResolvedGrid = null;
    }

    public bool TryResolve(out EntityUid grid)
    {
        if (_selectedGrid is not { } selected)
        {
            grid = EntityUid.Invalid;
            ResolvedGrid = null;
            return false;
        }

        grid = resolver(selected);
        if (grid == EntityUid.Invalid)
        {
            ResolvedGrid = null;
            return false;
        }

        ResolvedGrid = grid;
        return true;
    }
}
