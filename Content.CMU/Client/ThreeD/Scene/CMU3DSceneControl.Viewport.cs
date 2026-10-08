using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneControl : IViewportControl
{
    [Dependency] private IInputManager _input = default!;
    public int SceneDepth { get; set; }
    public readonly HashSet<MapId> SceneMaps = [];
    public MapId SceneMap { get; set; }
    public Vector2 SceneOrigin { get; set; }
    /// <summary>
    /// The original viewport supplies the affine map projection used by engine view-bounds queries.
    /// Visible pointer targeting belongs to PixelToMap/Aim and must never supply those corners.
    /// </summary>
    public IViewportControl? MapProjection { get; set; }
    public bool MouseCaptured { get; private set; }
    public event Action<float>? LookDelta;

    public void SetMouseCaptured(bool captured)
    {
        if (MouseCaptured == captured) return;
        MouseCaptured = _firstPersonLooking = captured;
        if (captured) LookStarted?.Invoke();
        else LookEnded?.Invoke();
    }

    public MapCoordinates Aim(Vector2 screen, out EntityUid? target)
    {
        var camera = Camera();
        var pixel = MouseCaptured ? camera.ScreenCenter : screen - GlobalPixelPosition;
        var ray = camera.RayDirection(pixel);
        var limit = FirstPerson ? VisibleRadius / Math.Max(.000001f, Math.Max(MathF.Abs(ray.X), MathF.Abs(ray.Y)))
            : float.PositiveInfinity;
        var found = _encoding.TryPick(camera.Origin, ray, out var hit, _surfaces) && hit.Distance <= limit;
        var distance = found ? hit.Distance : Math.Min(SceneRadius, limit);
        target = found ? hit.Source : null;
        PickBillboards(camera.Origin, ray, ref distance, ref target);
        PickEquipment(camera.Origin, ray, ref distance, ref target);
        var point = camera.Origin + ray * distance;
        var map = target is { } uid && _entities.TryGetComponent(uid, out TransformComponent? transform)
            ? transform.MapID : SceneMap;
        return new MapCoordinates(SceneOrigin + new Vector2(point.X, point.Y), map);
    }

    public MapCoordinates PixelToMap(Vector2 point) => Aim(point, out _);
    public MapCoordinates ScreenToMap(Vector2 point) => MapProjection?.ScreenToMap(point) ?? default;
    public Vector2 WorldToScreen(Vector2 map) => Camera().TryProject(new Vector3(map - SceneOrigin, 0), out var point)
        ? point + GlobalPixelPosition : new Vector2(-10000);
    // Legacy affine consumers use the same projection as ScreenToMap; visible points use WorldToScreen.
    public Matrix3x2 GetWorldToScreenMatrix() => MapProjection?.GetWorldToScreenMatrix() ?? Matrix3x2.Identity;
    public Matrix3x2 GetLocalToScreenMatrix() => Matrix3x2.CreateTranslation(GlobalPixelPosition);
}
