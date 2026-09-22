using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;

namespace Content.Client.CMU14.ZLevels.Core;

/// <summary>
/// Draws the actual airborne item above viewers below it, without moving its physics body.
/// </summary>
public sealed partial class CMUZOverheadItemOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private readonly CMUClientZLevelsSystem _zLevels;
    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CMUZOverheadItemOverlay()
    {
        IoCManager.InjectDependencies(this);
        _zLevels = _entities.System<CMUClientZLevelsSystem>();
        _maps = _entities.System<SharedMapSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _sprites = _entities.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled) ||
            args.Viewport.Eye is not { } eye ||
            !ShouldDrawForEye(eye) ||
            !_maps.TryGetMap(args.MapId, out var map))
        {
            return;
        }

        var handle = args.WorldHandle;
        try
        {
            var query = _entities.EntityQueryEnumerator<CMUZFallingComponent, ItemComponent, SpriteComponent>();
            while (query.MoveNext(out var uid, out _, out _, out var sprite))
            {
                if (!sprite.Visible ||
                    !_zLevels.TryGetOverheadItemProjection(uid, map.Value, out var coordinates, out var height))
                {
                    continue;
                }

                var position = coordinates.Position +
                    (-eye.Rotation).RotateVec(new Vector2(0f, height * CMUClientZLevelsSystem.ZLevelOffset));
                if (!args.WorldAABB.Enlarged(1f).Contains(position))
                    continue;

                var noRotation = sprite.NoRotation;
                try
                {
                    // Match ordinary Z elevation presentation, including rotated cameras.
                    sprite.NoRotation = true;
                    _sprites.RenderSprite((uid, sprite), handle, eye.Rotation,
                        _transform.GetWorldRotation(uid), position);
                }
                finally
                {
                    sprite.NoRotation = noRotation;
                }
            }
        }
        finally
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
        }
    }

    public static bool ShouldDrawForEye(IEye eye)
    {
        // Lower passes are composited underneath the highest visible map. Drawing there as
        // well would duplicate items already drawn normally by an upper pass when looking up.
        return eye is not ScalingViewport.ZEye zEye || zEye.Depth == zEye.HighestDepth;
    }
}
