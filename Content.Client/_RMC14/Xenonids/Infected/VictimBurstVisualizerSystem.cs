using Content.Shared._RMC14.Xenonids.Parasite;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Infected;

public sealed partial class VictimBurstVisualizerSystem : VisualizerSystem<VictimBurstComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, VictimBurstComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (!AppearanceSystem.TryGetData(uid, BurstVisuals.Visuals, out VictimBurstState state, args.Component))
            return;

        if (args.Sprite is not { } sprite)
            return;

        var rsiPath = component.RsiPath;

        var spriteState = state switch
        {
            VictimBurstState.Bursting => component.BurstingState,
            VictimBurstState.Burst => component.BurstState,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(spriteState))
            return;

        if (!_sprite.LayerMapTryGet((uid, sprite), BurstLayer.Base, out var layer, false))
        {
            layer = _sprite.LayerMapReserve((uid, sprite), BurstLayer.Base);
            _sprite.LayerSetRsi((uid, sprite), layer, rsiPath);
        }

        _sprite.LayerSetRsiState((uid, sprite), layer, spriteState);
    }
}
