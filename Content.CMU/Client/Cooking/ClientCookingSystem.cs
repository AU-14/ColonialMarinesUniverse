// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the cooking of crystall-punk-14 (MIT) and adapted to this project.
using Content.Shared.CMU14.Cooking;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Cooking;

// The look of the food: the layers of the dish are put over the vessel at the layer that it names.
public sealed class ClientCookingSystem : SharedCookingSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FoodHolderComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<FoodHolderComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<FoodHolderComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterHandleState(Entity<FoodHolderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<FoodHolderComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // The old layers go whatever the dish is now (a vessel that was emptied shows nothing).
        foreach (var key in ent.Comp.RevealedLayers)
            _sprite.RemoveLayer((ent, sprite), key, false);

        ent.Comp.RevealedLayers.Clear();

        if (ent.Comp.FoodData is null)
            return;

        _sprite.LayerMapTryGet((ent, sprite), ent.Comp.TargetLayerMap, out var index, false);

        var counter = 0;
        foreach (var layer in ent.Comp.FoodData.Visuals)
        {
            var layerIndex = index + counter;
            var key = $"au14-food-layer-{counter}";
            ent.Comp.RevealedLayers.Add(key);

            _sprite.AddBlankLayer((ent, sprite), layerIndex);
            _sprite.LayerMapSet((ent, sprite), key, layerIndex);
            _sprite.LayerSetData((ent, sprite), layerIndex, layer);
            counter++;
        }
    }
}
