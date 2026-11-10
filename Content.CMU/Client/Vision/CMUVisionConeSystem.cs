using System.Numerics;
using Content.Shared.CMU14.Vision;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Client.CMU14.Vision;

// marks a mob whose sprite we hid, so we only ever restore sprites we hid ourselves
[RegisterComponent]
public sealed partial class CMUVisionConeHiddenComponent : Component;

public sealed partial class CMUVisionConeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly CMUVisionConeComponent _defaultCone = new();

    public override void FrameUpdate(float frameTime)
    {
        var active = TryGetViewer(out var viewerPos, out var forward, out var viewerMap, out var cone);
        var cosHalfAngle = 0f;
        var nearSquared = 0f;

        if (active)
        {
            var halfAngle = Angle.FromDegrees(Math.Clamp(cone.Angle, 1f, 360f) / 2f);
            cosHalfAngle = (float) Math.Cos(halfAngle.Theta);
            var near = Math.Max(0f, cone.Radius);
            nearSquared = near * near;
        }

        var local = _player.LocalEntity;
        var mobs = EntityQueryEnumerator<MobStateComponent, SpriteComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var hide = active &&
                       uid != local &&
                       xform.MapID == viewerMap &&
                       !_container.IsEntityInContainer(uid) &&
                       !IsInCone(_transform.GetWorldPosition(xform) - viewerPos, forward, cosHalfAngle, nearSquared);

            var hidden = HasComp<CMUVisionConeHiddenComponent>(uid);
            if (hide && !hidden && sprite.Visible)
            {
                _sprite.SetVisible((uid, sprite), false);
                AddComp<CMUVisionConeHiddenComponent>(uid);
            }
            else if (!hide && hidden)
            {
                _sprite.SetVisible((uid, sprite), true);
                RemComp<CMUVisionConeHiddenComponent>(uid);
            }
        }
    }

    private bool TryGetViewer(out Vector2 position, out Vector2 forward,     out MapId map, out CMUVisionConeComponent cone)
    {
            cone = default!;
            position = default;
        forward = default;
        map = MapId.Nullspace;

        if (!_cfg.GetCVar(CMUVisionConeCVars.Enabled) ||
            _player.LocalEntity is not { } local ||
            !TryComp(local, out TransformComponent? xform) ||
            HasComp<GhostComponent>(local) ||
            !HasComp<MobStateComponent>(local) ||
            !_mobState.IsAlive(local))
        {
            return false;
        }

        // entities without the component fall back to the component's default values
        cone = CompOrNull<CMUVisionConeComponent>(local) ?? _defaultCone;
        position = _transform.GetWorldPosition(xform);
        forward = _transform.GetWorldRotation(xform).ToWorldVec();
        map = xform.MapID;
        return true;
    }

    private static bool IsInCone(Vector2 offset, Vector2 forward, float cosHalfAngle, float nearSquared)
    {
        var lengthSquared = offset.LengthSquared();
        if (lengthSquared <= nearSquared || lengthSquared <= float.Epsilon)
            return true;

        return Vector2.Dot(offset, forward) >= cosHalfAngle * MathF.Sqrt(lengthSquared);
    }
}
