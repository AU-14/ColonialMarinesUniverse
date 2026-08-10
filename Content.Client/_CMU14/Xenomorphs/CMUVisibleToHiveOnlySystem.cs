using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared._CMU14.Xenomorphs;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed partial class CMUVisibleToHiveOnlySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;

    public override void Update(float frameTime)
    {
        var local = _player.LocalEntity;

        var visibleQuery = EntityQueryEnumerator<CMUVisibleToHiveOnlyComponent, SpriteComponent>();

        while (visibleQuery.MoveNext(out var uid, out _, out var sprite))
        {
            var sameHive = local is { } localEnt &&
                _hive.GetHive(localEnt) is { } localHive &&
                _hive.GetHive(uid) is { } entHive &&
                localHive.Owner == entHive.Owner;

            var layerIndex = 0;
            foreach (var _ in sprite.AllLayers)
            {
                _sprite.LayerSetVisible((uid, sprite), layerIndex, sameHive);
                layerIndex++;
            }
        }
    }
}