using Content.Client.IconSmoothing;
using Content.Shared.Sprite;
using Robust.Client.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client._RMC14.IconSmoothing;

public sealed partial class IconSmoothRandomSystem : EntitySystem
{
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private EntityQuery<RandomSpriteComponent> _randomSpriteQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _randomSpriteQuery = GetEntityQuery<RandomSpriteComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<IconSmoothRandomComponent, IconSmoothingUpdatedEvent>(OnOverrideIconSmoothingUpdated);
    }

    private void OnOverrideIconSmoothingUpdated(Entity<IconSmoothRandomComponent> ent, ref IconSmoothingUpdatedEvent args)
    {
        if (!_randomSpriteQuery.TryGetComponent(ent, out var random) ||
            !_spriteQuery.TryGetComponent(ent, out var sprite))
            return;

        foreach (var layer in random.Selected)
        {
            int index;
            if (_reflection.TryParseEnumReference(layer.Key, out var @enum))
            {
                if (!_sprite.LayerMapTryGet((ent.Owner, sprite), @enum, out index, logMissing: true))
                    continue;
            }
            else if (!_sprite.LayerMapTryGet((ent.Owner, sprite), layer.Key, out index, false))
            {
                if (layer.Key is not { } strKey || !int.TryParse(strKey, out index))
                {
                    Log.Error($"Invalid key `{layer.Key}` for entity with random sprite {ToPrettyString(ent)}");
                    continue;
                }
            }

            var spriteName = _sprite.LayerGetRsiState((ent.Owner, sprite), index).Name;
            if (spriteName != null && ent.Comp.Overrides.Contains(spriteName))
            {
                _sprite.LayerSetRsiState((ent.Owner, sprite), index, layer.Value.State);
                _sprite.LayerSetColor((ent.Owner, sprite), index, layer.Value.Color ?? Color.White);
            }
        }
    }
}
