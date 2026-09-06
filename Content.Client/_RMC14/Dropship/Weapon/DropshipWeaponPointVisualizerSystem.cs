using System.Numerics;
using Content.Shared._CMU14.Dropship.DirectFire;
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
    [Dependency] private SharedTransformSystem _transform = default!;

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

        // The four RSI directions are front/rear, port/starboard mounting variants,
        // not world facings. Select the same variant this point used at its mapped
        // rotation, regardless of the dropship grid's current world rotation.
        spriteComp.EnableDirectionOverride = true;
        if (Transform(uid).GridUid is { } grid)
        {
            var relativeRotation = _transform.GetWorldRotation(uid) - _transform.GetWorldRotation(grid);
            spriteComp.DirectionOverride = relativeRotation.GetCardinalDir();
        }

        // Attachment points are wall-derived sprites. Render every layer with its
        // actual world transform so the point and installed weapon follow both the
        // dropship and the camera exactly once. NoRotation also prevents the RSI
        // direction matrix from counter-rotating the fixed mounting frame; the
        // per-layer Default strategy below still applies the entity's world rotation.
        spriteComp.NoRotation = true;
        spriteComp.GranularLayersRendering = true;
        _sprite.LayerSetRenderingStrategy((uid, spriteComp), 0, LayerRenderingStrategy.Default);
        _sprite.LayerSetRenderingStrategy((uid, spriteComp), layer, LayerRenderingStrategy.Default);

        if (string.IsNullOrWhiteSpace(sprite) || string.IsNullOrWhiteSpace(state))
        {
            _sprite.LayerSetVisible((uid, spriteComp), layer, false);
            return;
        }

        _sprite.LayerSetSprite((uid, spriteComp), layer, new SpriteSpecifier.Rsi(new ResPath(sprite), state));

        if (Enum.TryParse<DirectionOffset>(component.DirOffset, true, out var dir))
            _sprite.LayerSetDirOffset((uid, spriteComp), layer, dir);

        if (AppearanceSystem.TryGetData(uid,
                GunshipDirectFireVisuals.AimOffsetDegrees,
                out float aimOffsetDegrees,
                args.Component))
        {
            _sprite.LayerSetRotation((uid, spriteComp), layer, Angle.FromDegrees(aimOffsetDegrees));
        }

        if (TryComp(uid, out GunshipDirectFirePointComponent? directFirePoint))
        {
            // Preserve the attachment layer's original half-tile lateral
            // alignment. Only its forward coordinate is being extended.
            _sprite.LayerSetOffset((uid, spriteComp), layer, new Vector2(0.5f, directFirePoint.ForwardOffset));
        }

        _sprite.LayerSetVisible((uid, spriteComp), layer, true);
    }
}
