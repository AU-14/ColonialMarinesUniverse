using Content.Client.Administration.Managers;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaFalconVisibilitySystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        var local = _player.LocalEntity;
        var visible = local is { } viewer &&
                      (HasComp<YautjaComponent>(viewer) ||
                       HasComp<GhostComponent>(viewer) ||
                       _admin.IsAdmin());

        var query = EntityQueryEnumerator<YautjaFalconDroneDeployedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), YautjaFalconVisualLayers.Base, out var layer, true))
                continue;

            _sprite.LayerSetVisible((uid, sprite), layer, visible);
        }
    }
}
