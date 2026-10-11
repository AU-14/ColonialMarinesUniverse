using System.Numerics;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneControl
{
    /// <summary>Screen pixels above the visible sprite, using its floor and the perspective camera.</summary>
    public bool TryProjectHead(EntityUid uid, float extraHeight, out Vector2 pixel)
    {
        pixel = default;
        if (uid == _players.LocalEntity ||
            !_entities.TryGetComponent(uid, out TransformComponent? xform) || !SceneMaps.Contains(xform.MapID) ||
            !_entities.TryGetComponent(uid, out SpriteComponent? sprite) || !sprite.Visible || sprite.ContainerOccluded ||
            !_entities.TryGetComponent(uid, out MetaDataComponent? meta) ||
            (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0)
            return false;
        var camera = Camera();
        var transforms = _entities.System<SharedTransformSystem>();
        // Match the upright artwork rasterized into the mob's billboard atlas cell.
        var bounds = _entities.System<SpriteSystem>().CalculateBounds((uid, sprite), default, Angle.Zero, Angle.Zero).CalcBoundingBox();
        var head = new Vector3(transforms.GetWorldPosition(xform) - SceneOrigin,
            _entities.System<CMU3DElevationSystem>().PhysicalHeight(uid, SceneDepth) + bounds.Height * 1.6f + .15f + extraHeight);
        if (MathF.Abs(head.X - camera.Origin.X) > VisibleRadius || MathF.Abs(head.Y - camera.Origin.Y) > VisibleRadius ||
            !camera.TryProject(head, out pixel))
            return false;
        var delta = head - camera.Origin;
        var distance = delta.Length();
        if (_encoding.TryPick(camera.Origin, delta / distance, out var hit, _surfaces) &&
            hit.Source != uid && hit.Distance < distance - .1f)
            return false;
        pixel += GlobalPixelPosition;
        return true;
    }
}
