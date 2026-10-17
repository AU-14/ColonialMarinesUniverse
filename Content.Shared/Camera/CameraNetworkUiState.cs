using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Camera;

[Serializable, NetSerializable]
public enum CameraMapMarkerStatus : byte
{
    Active,
    Inactive,
    Invalid,
}

[Serializable, NetSerializable]
public sealed class CameraMapMarkerUiData(NetEntity camera, Vector2 position, string name, CameraMapMarkerStatus status)
{
    public CameraMapMarkerUiData(NetEntity camera, Vector2 position, string name, bool active)
        : this(camera, position, name, active ? CameraMapMarkerStatus.Active : CameraMapMarkerStatus.Inactive)
    {
    }

    public NetEntity Camera { get; } = camera;
    public Vector2 Position { get; } = position;
    public string Name { get; } = name;
    public CameraMapMarkerStatus Status { get; } = status;
    public bool Active => Status == CameraMapMarkerStatus.Active;
}

[Serializable, NetSerializable]
public sealed class CameraMapGridUiData(NetEntity grid, string name, List<CameraMapMarkerUiData> markers)
{
    public NetEntity Grid { get; } = grid;
    public string Name { get; } = name;
    public List<CameraMapMarkerUiData> Markers { get; } = markers;
}

[Serializable, NetSerializable]
public sealed class CameraMapUiState(NetEntity? consoleGrid, List<CameraMapGridUiData> grids)
{
    public NetEntity? ConsoleGrid { get; } = consoleGrid;
    public List<CameraMapGridUiData> Grids { get; } = grids;
}
