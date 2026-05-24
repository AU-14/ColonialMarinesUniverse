using Content.Shared._RMC14.Entrenching;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using static Content.Shared._RMC14.Entrenching.EntrenchingToolComponentVisualLayers;

namespace Content.Client._RMC14.Entrenching;

public sealed partial class EntrenchingToolVisualsSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EntrenchingToolComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<EntrenchingToolComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnHandleState(Entity<EntrenchingToolComponent> tool, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(tool);
    }

    private void OnAppearanceChange(Entity<EntrenchingToolComponent> tool, ref AppearanceChangeEvent args)
    {
        UpdateVisuals(tool);
    }

    private void UpdateVisuals(Entity<EntrenchingToolComponent> tool)
    {
        if (!TryComp(tool, out SpriteComponent? sprite))
            return;

        if (_appearance.TryGetData(tool, ToggleableVisuals.Enabled, out bool toggled) && toggled)
        {
            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Base, out var baseLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), baseLayer, true);

            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Folded, out var foldedLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), foldedLayer, false);

            if (tool.Comp.TotalLayers > 0)
            {
                if (_sprite.LayerMapTryGet((tool.Owner, sprite), Dirt, out var dirtLayer, false))
                {
                    _sprite.LayerSetVisible((tool.Owner, sprite), dirtLayer, true);

                    // TODO RMC14 color per dirt type
                    _sprite.LayerSetColor((tool.Owner, sprite), dirtLayer, Color.FromHex("#C04000"));
                }
                else
                {
                    _sprite.LayerSetVisible((tool.Owner, sprite), dirtLayer, false);
                }
            }
            else
            {
                if (_sprite.LayerMapTryGet((tool.Owner, sprite), Dirt, out var dirtLayer, false))
                    _sprite.LayerSetVisible((tool.Owner, sprite), dirtLayer, false);
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Base, out var baseLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), baseLayer, false);

            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Folded, out var foldedLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), foldedLayer, true);

            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Dirt, out var dirtLayer, false))
            {
                _sprite.LayerSetVisible((tool.Owner, sprite), dirtLayer, false);
            }
        }
    }
}
