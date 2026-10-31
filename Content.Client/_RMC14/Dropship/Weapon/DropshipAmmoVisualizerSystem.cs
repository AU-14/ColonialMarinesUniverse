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

        // CMU14: M90 crates fire individual shells but retain their ten-stage fill sprites.
        var step = component.RoundsPerSprite ?? component.RoundsPerShot;
        var fillNum = component.RoundsPerSprite != null
            ? Math.Clamp((int) MathF.Ceiling((float) fill / step), 0,
                (int) MathF.Ceiling((float) component.MaxRounds / step))
            : Math.Clamp(fill / step, 0, component.MaxRounds / step);
        var state = (component.AmmoSpriteType ?? component.AmmoType) + "_" + fillNum;

        _sprite.LayerSetRsiState((uid, spriteComp), layer, state);
    }
}
