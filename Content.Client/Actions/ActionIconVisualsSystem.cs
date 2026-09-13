using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Actions;

public sealed partial class ActionIconVisualsSystem : VisualizerSystem<ActionComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DynamicActionIconComponent, ActionVisualsShutdownEvent>(OnVisualsShutdown);
    }

    private void OnVisualsShutdown(Entity<DynamicActionIconComponent> ent, ref ActionVisualsShutdownEvent args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp<SpriteComponent>(ent, out var sprite))
        {
            if (ent.Comp.CreatedLayer && SpriteSystem.LayerMapTryGet((ent.Owner, sprite), ActionVisuals.IconToggled, out var index, false))
                SpriteSystem.RemoveLayer((ent.Owner, sprite), index);
            else if (ent.Comp.OverrideApplied)
                RestoreToggledIcon((ent.Owner, sprite), ent.Comp);
        }

        RemComp<DynamicActionIconComponent>(ent);
    }

    private void RestoreToggledIcon(Entity<SpriteComponent?> sprite, DynamicActionIconComponent icon)
    {
        SpriteSystem.LayerSetTexture(sprite, ActionVisuals.IconToggled, icon.OriginalTexture);
        if (icon.OriginalState.IsValid)
            SpriteSystem.LayerSetRsi(sprite, ActionVisuals.IconToggled, icon.OriginalRsi, icon.OriginalState);
        icon.OverrideApplied = false;
    }

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
            if (!TryComp<DynamicActionIconComponent>(uid, out var dynamicIcon))
            {
                dynamicIcon = AddComp<DynamicActionIconComponent>(uid);
                dynamicIcon.CreatedLayer = !SpriteSystem.LayerMapTryGet(sprite, ActionVisuals.IconToggled, out var originalIndex, false);
                if (!dynamicIcon.CreatedLayer && SpriteSystem.TryGetLayer(sprite, originalIndex, out var original, false))
                {
                    dynamicIcon.OriginalTexture = original.Texture;
                    dynamicIcon.OriginalRsi = original.ActualRsi;
                    dynamicIcon.OriginalState = SpriteSystem.LayerGetRsiState(sprite, originalIndex);
                }
            }
            dynamicIcon.OverrideApplied = true;
            SpriteSystem.LayerMapReserve((uid, args.Sprite), ActionVisuals.IconToggled);

            if (toggledIcon is SpriteSpecifier.EntityPrototype)
                SpriteSystem.LayerSetTexture(
                    (uid, args.Sprite),
                    ActionVisuals.IconToggled,
                    SpriteSystem.Frame0(toggledIcon));
            else
                SpriteSystem.LayerSetSprite((uid, args.Sprite), ActionVisuals.IconToggled, toggledIcon);
        }
        else if (TryComp<DynamicActionIconComponent>(uid, out var dynamicIcon) && dynamicIcon.OverrideApplied)
        {
            RestoreToggledIcon(sprite, dynamicIcon);
        }

        if (!AppearanceSystem.TryGetData<bool>(uid, ActionState.Toggled, out var toggled, args.Component))
            toggled = comp.Toggled;

        var hasToggledLayer = SpriteSystem.LayerMapTryGet(sprite, ActionVisuals.IconToggled, out var toggledLayer, false);
        var hasToggledIcon = hasToggledLayer && SpriteSystem.TryGetLayer(sprite, toggledLayer, out var layer, false) && !layer.Blank;
        SpriteSystem.LayerSetVisible((uid, args.Sprite), ActionVisuals.Icon, !toggled || !hasToggledIcon);

        if (hasToggledLayer)
            SpriteSystem.LayerSetVisible((uid, args.Sprite), ActionVisuals.IconToggled, toggled && hasToggledIcon);

        if (AppearanceSystem.TryGetData<Color>(uid, ActionState.Color, out var color, args.Component))
        {
            SpriteSystem.LayerSetColor((uid, args.Sprite), ActionVisuals.Icon, color);

            if (SpriteSystem.LayerExists((uid, args.Sprite), ActionVisuals.IconToggled))
                SpriteSystem.LayerSetColor((uid, args.Sprite), ActionVisuals.IconToggled, color);
        }
    }
}
