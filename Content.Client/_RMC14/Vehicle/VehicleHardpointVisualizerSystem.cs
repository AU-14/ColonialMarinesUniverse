using System.Collections.Generic;
using Content.Shared._RMC14.Vehicle;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._RMC14.Vehicle;

public sealed partial class VehicleHardpointVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleHardpointVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VehicleHardpointVisualsComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnStartup(EntityUid uid, VehicleHardpointVisualsComponent component, ref ComponentStartup args)
    {
        ApplyLayers(uid, component);
    }

    private void OnHandleState(EntityUid uid, VehicleHardpointVisualsComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not VehicleHardpointVisualsComponentState state)
            return;

        component.Layers = new List<VehicleHardpointLayerState>(state.Layers);
        ApplyLayers(uid, component);
    }

    private void ApplyLayers(EntityUid uid, VehicleHardpointVisualsComponent component)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        foreach (var entry in component.Layers)
        {
            UpdateLayer(uid, sprite, entry.Layer, entry.State);
        }
    }

    private void UpdateLayer(EntityUid uid, SpriteComponent sprite, string layerMap, string state)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerMap, out var layer, false))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        _sprite.LayerSetRsiState((uid, sprite), layer, state);
        _sprite.LayerSetVisible((uid, sprite), layer, true);
    }
}
