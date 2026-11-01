using Content.Client.Toggleable;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Light.Components;

namespace Content.Client.CMU14.Light;

// light helmets (prodigy helmet, hardhats) draw their "on" state as a whole new helmet layer.
// ToggleableVisuals and the garb/visor/stain overlays all run "after clothing" with no order between them,
// so the lit helmet could land on top and hide nvgs, headphones etc. we tuck the toggle layers back
// right above the base clothing layer so every overlay still draws over it.
public sealed partial class CMUToggleLightLayerOrderSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // ToggleableVisualsComponent already owns this event, so hang off the light instead
        SubscribeLocalEvent<HandheldLightComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ToggleableVisualsSystem)]);
    }

    private void OnGetEquipmentVisuals(Entity<HandheldLightComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!TryComp(ent, out ToggleableVisualsComponent? toggle) ||
            toggle.ClothingVisuals.Count == 0 ||
            args.Layers.Count < 2)
        {
            return;
        }

        // ToggleableVisuals adds the exact layer objects from its own dict, so match by reference
        var toggleLayers = new List<(string, PrototypeLayerData)>();
        for (var i = args.Layers.Count - 1; i >= 0; i--)
        {
            if (!IsToggleLayer(toggle, args.Layers[i].Item2))
                continue;

            toggleLayers.Insert(0, args.Layers[i]);
            args.Layers.RemoveAt(i);
        }

        if (toggleLayers.Count == 0)
            return;

        // MappedLayer is the last key the base clothing layer wrote this pass; missing = no base layer, go first
        var insertAt = 0;
        if (TryComp(ent, out ClothingComponent? clothing) && clothing.MappedLayer is { } mapped)
        {
            var baseIndex = args.Layers.FindIndex(l => l.Item1 == mapped);
            if (baseIndex >= 0)
                insertAt = baseIndex + 1;
        }

        args.Layers.InsertRange(insertAt, toggleLayers);
    }

    private static bool IsToggleLayer(ToggleableVisualsComponent toggle, PrototypeLayerData layer)
    {
        foreach (var layers in toggle.ClothingVisuals.Values)
        {
            foreach (var candidate in layers)
            {
                if (ReferenceEquals(candidate, layer))
                    return true;
            }
        }

        return false;
    }
}
