using Content.Shared.CMU14.PowerLoader;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.PowerLoader;

/// <summary>
/// Keeps a <see cref="CMUStrapLightComponent"/> light on the beacon drawn on its no-rotation sprite.
/// Point light offsets turn with the entity, so the on-screen offset is counter-rotated by the entity's
/// on-screen angle, the same angle the sprite uses to pick its direction.
/// </summary>
public sealed partial class CMUStrapLightOffsetSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void FrameUpdate(float frameTime)
    {
        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<CMUStrapLightComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var strapLight, out var light, out var xform))
        {
            if (!light.Enabled)
                continue;

            var screenRotation = _transform.GetWorldRotation(xform) + eyeRotation;
            var screenOffset = strapLight.Offsets.GetValueOrDefault(screenRotation.GetCardinalDir());
            _pointLight.SetOffset(uid, (-screenRotation).RotateVec(screenOffset), light);
        }
    }
}
