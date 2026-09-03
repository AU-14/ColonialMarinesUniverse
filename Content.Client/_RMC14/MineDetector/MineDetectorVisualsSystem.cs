using Content.Shared._RMC14.MineDetector;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Item;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using static Content.Shared._RMC14.MineDetector.MineDetectorVisualLayers;

namespace Content.Client._RMC14.MineDetector;

public sealed partial class MineDetectorVisualsSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MineDetectorComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<MineDetectorComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<MineDetectorComponent, GetInhandVisualsEvent>(OnGetInhandVisuals);
    }

    private void OnHandleState(Entity<MineDetectorComponent> tool, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(tool);
    }

    private void OnAppearanceChange(Entity<MineDetectorComponent> tool, ref AppearanceChangeEvent args)
    {
        UpdateVisuals(tool);
    }

    private void UpdateVisuals(Entity<MineDetectorComponent> tool)
    {
        if (!TryComp(tool, out SpriteComponent? sprite))
            return;

        if (_appearance.TryGetData(tool, ToggleableVisuals.Enabled, out bool toggled) && toggled)
        {
            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Base, out var baseLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), baseLayer, true);

            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Folded, out var foldedLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), foldedLayer, false);
        }
        else
        {
            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Base, out var baseLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), baseLayer, false);

            if (_sprite.LayerMapTryGet((tool.Owner, sprite), Folded, out var foldedLayer, false))
                _sprite.LayerSetVisible((tool.Owner, sprite), foldedLayer, true);
        }

        _item.VisualsChanged(tool.Owner);
    }

    private void OnGetInhandVisuals(Entity<MineDetectorComponent> tool, ref GetInhandVisualsEvent args)
    {
        if (!TryComp(tool, out AppearanceComponent? appearance))
            return;

        var enabled = _appearance.TryGetData(tool, ToggleableVisuals.Enabled, out bool toggled) && toggled;

        if (enabled)
        {
            // Unfolded visuals
            if (args.Location == HandLocation.Left)
                args.Layers.Add(("inhand-left", new PrototypeLayerData { State = "minedetector_l" }));
            else if (args.Location == HandLocation.Right)
                args.Layers.Add(("inhand-right", new PrototypeLayerData { State = "minedetector_r" }));
        }
        else
        {
            // Folded visuals
            if (args.Location == HandLocation.Left)
                args.Layers.Add(("inhand-left", new PrototypeLayerData { State = "minedetector_folded_l" }));
            else if (args.Location == HandLocation.Right)
                args.Layers.Add(("inhand-right", new PrototypeLayerData { State = "minedetector_folded_r" }));
        }
    }
}
