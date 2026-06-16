using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Damage;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Damage;

public sealed partial class RMCXenoDamageVisualsSystem : VisualizerSystem<RMCXenoDamageVisualsComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    // CMU14
    public static int GetDamageVisualState(int states, int level)
    {
        if (states <= 0)
            return 0;

        return Math.Clamp(states - level + 1, 1, states);
    }
    // CMU14

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<RMCXenoDamageVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), RMCDamageVisualLayers.Base, out var layer, false))
                continue;

            SyncDamageAnimation((uid, sprite), layer);
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, RMCXenoDamageVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite == null ||
            !AppearanceSystem.TryGetData(uid, RMCDamageVisuals.State, out int level) ||
            !_sprite.LayerMapTryGet((uid, sprite), RMCDamageVisualLayers.Base, out var layer, false))
        {
            return;
        }

        if (level == 0)
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        _sprite.LayerSetVisible((uid, sprite), layer, true);

        // CMU14
        var state = GetDamageVisualState(component.States, level);
        // CMU14
        string rsiState;
        if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Downed, out bool downed) && downed)
        {
            rsiState = $"{component.Prefix}_downed_{state}";
        }
        else if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Fortified, out bool fortified) && fortified)
        {
            rsiState = $"{component.Prefix}_fortify_{state}";
        }
        else if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Resting, out bool resting) && resting)
        {
            rsiState = $"{component.Prefix}_rest_{state}";
        }
        else
        {
            rsiState = $"{component.Prefix}_walk_{state}";
        }

        // CMU14
        if (!HasDamageVisualState(sprite, layer, rsiState))
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }
        // CMU14

        _sprite.LayerSetRsiState((uid, sprite), layer, rsiState);
        SyncDamageAnimation((uid, sprite), layer);
    }

    // CMU14
    private static bool HasDamageVisualState(SpriteComponent sprite, int layer, string rsiState)
    {
        var spriteLayer = sprite[layer];
        var rsi = spriteLayer.ActualRsi ?? sprite.BaseRSI;

        return rsi != null && rsi.TryGetState(rsiState, out _);
    }
    // CMU14

    private void SyncDamageAnimation(Entity<SpriteComponent> spriteEnt, int damageLayer)
    {
        if (!_sprite.LayerMapTryGet(spriteEnt.AsNullable(), XenoVisualLayers.Base, out var baseLayer, false))
            return;

        var baseSpriteLayer = spriteEnt.Comp[baseLayer];
        var damageSpriteLayer = spriteEnt.Comp[damageLayer];
        if (!baseSpriteLayer.Visible ||
            !damageSpriteLayer.Visible ||
            MathHelper.CloseTo(baseSpriteLayer.AnimationTime, damageSpriteLayer.AnimationTime, 0.001f))
        {
            return;
        }

        _sprite.LayerSetAnimationTime(spriteEnt.AsNullable(), damageLayer, baseSpriteLayer.AnimationTime);
    }
}
