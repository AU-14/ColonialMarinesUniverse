using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Dropship.Weapon;

public sealed partial class DropshipAmmoVisualizerSystem : VisualizerSystem<DropshipAmmoComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, DropshipAmmoComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (args.Sprite is not { } spriteComp)
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, DropshipAmmoVisuals.Fill, out var fill, args.Component))
            return;

        if (!_sprite.LayerMapTryGet((uid, spriteComp), DropshipAmmoVisuals.Fill, out var layer, false))
            return;

        if (component.AmmoType == null)
            return;

        var fillNum = Math.Clamp(fill / component.RoundsPerShot, 0, component.MaxRounds / component.RoundsPerShot);
        var state = component.AmmoType + "_" + fillNum;

        _sprite.LayerSetRsiState((uid, spriteComp), layer, state);
    }
}
