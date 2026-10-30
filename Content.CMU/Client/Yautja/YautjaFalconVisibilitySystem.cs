using Content.Shared.CMU14.Yautja;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaFalconVisibilitySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var local = _player.LocalEntity;
        var visible = local is { } viewer &&
                      (HasComp<YautjaComponent>(viewer) ||
                       HasComp<GhostComponent>(viewer));

        var query = EntityQueryEnumerator<YautjaFalconDroneDeployedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), YautjaFalconVisualLayers.Base, out var layer, true))
                continue;

            var deploying = TryComp<CMUYautjaMotionComponent>(uid, out var motion) &&
                            motion.Kind == CMUYautjaEffect.FalconDeploy && _timing.CurTime < motion.EndsAt;
            _sprite.LayerSetVisible((uid, sprite), layer, visible && !deploying);
        }
    }
}
