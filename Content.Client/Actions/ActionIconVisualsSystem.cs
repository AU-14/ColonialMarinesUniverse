using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Actions;

public sealed partial class ActionIconVisualsSystem : VisualizerSystem<ActionComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ActionComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        // CMU14: Action prototypes often replace inherited sprite layers without repeating the icon map.
        if (!SpriteSystem.LayerMapTryGet(sprite, ActionVisuals.Icon, out _, false))
        {
            if (SpriteSystem.LayerExists(sprite, 0))
                SpriteSystem.LayerMapSet(sprite, ActionVisuals.Icon, 0);
            else
                SpriteSystem.LayerMapReserve(sprite, ActionVisuals.Icon);
        }

        if (AppearanceSystem.TryGetData<SpriteSpecifier>(uid, ActionState.DynamicIcon, out var icon, args.Component))
        {
            if (icon is SpriteSpecifier.EntityPrototype)
                SpriteSystem.LayerSetTexture((uid, args.Sprite), ActionVisuals.Icon, SpriteSystem.Frame0(icon));
            else
                SpriteSystem.LayerSetSprite((uid, args.Sprite), ActionVisuals.Icon, icon);
        }

        if (AppearanceSystem.TryGetData<SpriteSpecifier>(
                uid,
                ActionState.DynamicIconToggled,
                out var toggledIcon,
                args.Component))
        {
            SpriteSystem.LayerMapReserve((uid, args.Sprite), ActionVisuals.IconToggled);

            if (toggledIcon is SpriteSpecifier.EntityPrototype)
                SpriteSystem.LayerSetTexture(
                    (uid, args.Sprite),
                    ActionVisuals.IconToggled,
                    SpriteSystem.Frame0(toggledIcon));
            else
                SpriteSystem.LayerSetSprite((uid, args.Sprite), ActionVisuals.IconToggled, toggledIcon);
        }

        if (!AppearanceSystem.TryGetData<bool>(uid, ActionState.Toggled, out var toggled, args.Component))
            toggled = comp.Toggled;

        var hasToggledIcon = SpriteSystem.LayerExists((uid, args.Sprite), ActionVisuals.IconToggled);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), ActionVisuals.Icon, !toggled || !hasToggledIcon);

        if (hasToggledIcon)
            SpriteSystem.LayerSetVisible((uid, args.Sprite), ActionVisuals.IconToggled, toggled);

        if (AppearanceSystem.TryGetData<Color>(uid, ActionState.Color, out var color, args.Component))
        {
            SpriteSystem.LayerSetColor((uid, args.Sprite), ActionVisuals.Icon, color);

            if (SpriteSystem.LayerExists((uid, args.Sprite), ActionVisuals.IconToggled))
                SpriteSystem.LayerSetColor((uid, args.Sprite), ActionVisuals.IconToggled, color);
        }
    }
}
