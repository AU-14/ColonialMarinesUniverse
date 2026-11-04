using Content.Shared.CMU14.Fob;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Fob;

public sealed class CMUFobEquipmentVisualsSystem : VisualizerSystem<CMUFobEquipmentComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, CMUFobEquipmentComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null && AppearanceSystem.TryGetData<string>(uid, CMUFobEquipmentVisuals.State, out var state, args.Component))
        {
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), 0, state);
            if (SpriteSystem.LayerExists(uid, "fob-effect"))
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), "fob-effect", state);
        }
    }
}
