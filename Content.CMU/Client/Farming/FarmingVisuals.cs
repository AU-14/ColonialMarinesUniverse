// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the farming of crystall-punk-14 (MIT / CC-BY-SA) and adapted to this project.
using Content.Shared.CMU14.Farming;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Farming;

// Client side of farming: sets the sprite of a plant to the state of its growth.
public sealed class ClientFarmingSystem : SharedFarmingSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FarmPlantVisualsComponent, ComponentInit>(OnPlantVisualInit);
        SubscribeLocalEvent<FarmPlantComponent, AfterAutoHandleStateEvent>(OnAutoHandleState);
    }

    private void OnAutoHandleState(Entity<FarmPlantComponent> plant, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<FarmPlantVisualsComponent>(plant, out var visuals))
            UpdateVisuals((plant, visuals));
    }

    private void OnPlantVisualInit(Entity<FarmPlantVisualsComponent> visuals, ref ComponentInit args)
    {
        UpdateVisuals(visuals);
    }

    private void UpdateVisuals(Entity<FarmPlantVisualsComponent> visuals)
    {
        if (!TryComp<SpriteComponent>(visuals, out var sprite) || !PlantQuery.TryComp(visuals, out var plant))
            return;

        var growthState = ContentHelpers.RoundToNearestLevels(plant.GrowthLevel, 1, visuals.Comp.GrowthSteps);
        if (growthState == 0)
            growthState++;

        if (_sprite.LayerMapTryGet((visuals, sprite), FarmPlantVisualLayers.Base, out var layer, false))
            _sprite.LayerSetRsiState((visuals, sprite), layer, $"{visuals.Comp.GrowState}{growthState}");
    }
}
