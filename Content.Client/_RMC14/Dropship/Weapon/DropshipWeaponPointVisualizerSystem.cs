using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._RMC14.Dropship.Weapon;

public sealed partial class DropshipWeaponPointVisualizerSystem : VisualizerSystem<DropshipWeaponPointComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, DropshipWeaponPointComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);
        if (args.Sprite is not { } spriteComp)
            return;

        if (!AppearanceSystem.TryGetData(uid, DropshipWeaponVisuals.Sprite, out string? sprite, args.Component) ||
            !AppearanceSystem.TryGetData(uid, DropshipWeaponVisuals.State, out string? state, args.Component))
        {
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, spriteComp), DropshipWeaponPointLayers.Layer, out var layer, false))
            return;

        if (string.IsNullOrWhiteSpace(sprite) || string.IsNullOrWhiteSpace(state))
        {
            _sprite.LayerSetVisible((uid, spriteComp), layer, false);
            return;
        }

        _sprite.LayerSetSprite((uid, spriteComp), layer, new SpriteSpecifier.Rsi(new ResPath(sprite), state));

        if (Enum.TryParse<DirectionOffset>(component.DirOffset, true, out var dir))
            _sprite.LayerSetDirOffset((uid, spriteComp), layer, dir);

        _sprite.LayerSetVisible((uid, spriteComp), layer, true);
    }
}
