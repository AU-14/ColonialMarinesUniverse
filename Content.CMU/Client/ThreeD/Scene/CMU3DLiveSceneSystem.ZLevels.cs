using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private readonly HashSet<EntityUid> _zStairEntities = [];

    private bool TryAddZStair(EntityUid uid, Vector2 origin, out bool admitted)
    {
        admitted = false;
        if (!TryComp(uid, out CMUZLevelHighGroundComponent? ground) || !ground.Stick ||
            ground.SupportOnlyFromAbove || ground.HeightCurve.Count < 2 ||
            ground.HeightCurve.Max() - ground.HeightCurve.Min() <= .01f ||
            !TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            xform.MapUid is not { } map || xform.GridUid is not { } grid ||
            !TryComp(grid, out MapGridComponent? gridComp) || gridComp.TileSize != 1)
            return false;
        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var world = _map.GridTileToWorldPos(grid, gridComp, tile);
        var lower = _elevation.GroundAt(map, world);
        var upper = _elevation.GroundOffset(map, 1, world);
        var parts = CMU3DZProjection.StairParts(ground.HeightCurve,
            _transform.GetWorldRotation(uid).GetCardinalDir(), ground.Corner, lower, upper);
        if (_elevation.TryStair(uid, out var fitted) && fitted.PhysicsCurve.Count > 0)
            parts = CMU3DZProjection.StairParts([fitted.Top / CMU3DZProjection.StoryHeight,
                fitted.Bottom / CMU3DZProjection.StoryHeight],
                new Angle(MathF.Atan2(-fitted.Direction.X, fitted.Direction.Y)).GetCardinalDir(), false,
                0, 0);
        _entityBoxes.Clear();
        var yaw = (float) _transform.GetWorldRotation(grid).Theta;
        foreach (var part in parts)
        {
            var center = (part.Min + part.Max) / 2;
            var xy = new Angle(yaw).RotateVec(new Vector2(center.X, center.Y));
            _entityBoxes.Add(new CMU3DSceneBox(new Vector3(world - origin + xy, center.Z),
                (part.Max - part.Min) / 2, yaw, part.Color, uid));
        }
        if (_boxes.Count + _entityBoxes.Count <= ViewPartLimit)
        {
            _boxes.AddRange(_entityBoxes);
            _zStairEntities.Add(uid);
            admitted = true;
        }
        else
            _mapRejectedParts += _entityBoxes.Count;
        return true;
    }
}
