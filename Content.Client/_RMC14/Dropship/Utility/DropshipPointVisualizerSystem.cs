using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._RMC14.Dropship.Utility;

public sealed partial class DropshipPointVisualizerSystem : VisualizerSystem<DropshipPointVisualsComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void OnAppearanceChange(EntityUid uid, DropshipPointVisualsComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);
        if (args.Sprite is not { } spriteComp)
            return;

        if (!AppearanceSystem.TryGetData(uid, DropshipUtilityVisuals.Sprite, out string? sprite, args.Component) ||
            !AppearanceSystem.TryGetData(uid, DropshipUtilityVisuals.State, out string? state, args.Component))
        {
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, spriteComp), DropshipPointVisualsLayers.AttachmentBase, out var attachmentBase, false))
            return;

        // Utility and electronic points use the same slot-relative sprite setup as
        // weapon points. Select directional mounting variants in dropship-local
        // space, then let normal world rendering handle ship and camera rotation.
        spriteComp.EnableDirectionOverride = true;
        if (Transform(uid).GridUid is { } grid)
        {
            var relativeRotation = _transform.GetWorldRotation(uid) - _transform.GetWorldRotation(grid);
            spriteComp.DirectionOverride = relativeRotation.GetCardinalDir();
        }

        // Suppress the automatic directional-frame counter-rotation while retaining
        // world rotation through the granular Default layer strategies. This makes
        // camera rotation affect the point exactly as it affects the dropship.
        spriteComp.NoRotation = true;
        spriteComp.GranularLayersRendering = true;
        _sprite.LayerSetRenderingStrategy((uid, spriteComp), 0, LayerRenderingStrategy.Default);
        _sprite.LayerSetRenderingStrategy((uid, spriteComp), attachmentBase, LayerRenderingStrategy.Default);

        if (!_sprite.LayerMapTryGet((uid, spriteComp), DropshipPointVisualsLayers.AttachedUtility, out var attachedUtility, false))
        {
            _sprite.LayerSetVisible((uid, spriteComp), attachmentBase, true);
            //spriteComp.LayerSetVisible(attachedUtility, false);
            return;
        }

        _sprite.LayerSetRenderingStrategy((uid, spriteComp), attachedUtility, LayerRenderingStrategy.Default);

        if (string.IsNullOrWhiteSpace(sprite) || string.IsNullOrWhiteSpace(state))
        {
            _sprite.LayerSetVisible((uid, spriteComp), attachmentBase, true);
            _sprite.LayerSetVisible((uid, spriteComp), attachedUtility, false);
            return;
        }

        _sprite.LayerSetSprite((uid, spriteComp), attachedUtility, new SpriteSpecifier.Rsi(new ResPath(sprite), state));

        //if (Enum.TryParse<SpriteComponent.DirectionOffset>(component.DirOffset, true, out var dir))
        //spriteComp.LayerSetDirOffset(layer, dir);
        _sprite.LayerSetVisible((uid, spriteComp), attachmentBase, false);
        _sprite.LayerSetVisible((uid, spriteComp), attachedUtility, true);
    }
}
