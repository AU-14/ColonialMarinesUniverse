using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Actions;

public sealed partial class ActionIconVisualsSystem : VisualizerSystem<ActionComponent>
{
    private readonly HashSet<EntityUid> _dynamicToggledIcons = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionComponent, ActionVisualsShutdownEvent>(OnActionShutdown);
    }

    private void OnActionShutdown(Entity<ActionComponent> ent, ref ActionVisualsShutdownEvent args)
    {
        if (!_dynamicToggledIcons.Remove(ent.Owner) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        SpriteSystem.RemoveLayer((ent.Owner, sprite), ActionVisuals.IconToggled, false);
    }

    protected override void OnAppearanceChange(EntityUid uid, ActionComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = new Entity<SpriteComponent>(uid, args.Sprite);

        if (AppearanceSystem.TryGetData<SpriteSpecifier>(uid, ActionState.DynamicIcon, out var icon, args.Component))
        {
            if (icon is SpriteSpecifier.EntityPrototype)
                SpriteSystem.LayerSetTexture(sprite, ActionVisuals.Icon, SpriteSystem.Frame0(icon));
            else
                SpriteSystem.LayerSetSprite(sprite, ActionVisuals.Icon, icon);
        }

        UpdateToggledIcon(sprite, args.Component);

        if (AppearanceSystem.TryGetData<Color>(uid, ActionState.Color, out var color, args.Component))
        {
            SpriteSystem.LayerSetColor(sprite, ActionVisuals.Icon, color);

            if (SpriteSystem.LayerExists(sprite, ActionVisuals.IconToggled))
                SpriteSystem.LayerSetColor(sprite, ActionVisuals.IconToggled, color);
        }
    }

    private void UpdateToggledIcon(Entity<SpriteComponent> sprite, AppearanceComponent appearance)
    {
        if (!AppearanceSystem.TryGetData<SpriteSpecifier>(
                sprite.Owner,
                ActionState.DynamicIconToggled,
                out var icon,
                appearance))
        {
            if (_dynamicToggledIcons.Contains(sprite.Owner) &&
                SpriteSystem.LayerExists(sprite, ActionVisuals.IconToggled))
                SpriteSystem.LayerSetTexture(sprite, ActionVisuals.IconToggled, null);

            return;
        }

        if (!SpriteSystem.LayerExists(sprite, ActionVisuals.IconToggled))
        {
            var layer = icon is SpriteSpecifier.EntityPrototype
                ? SpriteSystem.AddTextureLayer(sprite, SpriteSystem.Frame0(icon))
                : SpriteSystem.AddLayer(sprite, icon);

            SpriteSystem.LayerMapSet(sprite, ActionVisuals.IconToggled, layer);

            var visible = AppearanceSystem.TryGetData<bool>(
                sprite.Owner,
                ActionState.Toggled,
                out var toggled,
                appearance) && toggled;
            SpriteSystem.LayerSetVisible(sprite, layer, visible);
        }
        else if (icon is SpriteSpecifier.EntityPrototype)
        {
            SpriteSystem.LayerSetTexture(sprite, ActionVisuals.IconToggled, SpriteSystem.Frame0(icon));
        }
        else
        {
            SpriteSystem.LayerSetSprite(sprite, ActionVisuals.IconToggled, icon);
        }

        _dynamicToggledIcons.Add(sprite.Owner);
    }
}
