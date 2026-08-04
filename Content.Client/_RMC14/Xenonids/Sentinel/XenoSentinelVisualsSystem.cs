using System.Linq;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Sentinel;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Sentinel;

public sealed partial class XenoSentinelVisualsSystem : EntitySystem
{
    private static readonly Color DrainSurgeColor = Color.FromHex("#7FFF00");

    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, DrainSurgeOriginalColor> _drainSurgeOriginalColors = new();
    private readonly List<EntityUid> _remove = new();

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XenoDrainSurgeComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            _drainSurgeOriginalColors.TryAdd(uid, GetOriginalColors(uid, sprite));

            if (sprite.Color != DrainSurgeColor)
                _sprite.SetColor((uid, sprite), DrainSurgeColor);

            if (_sprite.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var baseLayer, false))
                _sprite.LayerSetColor((uid, sprite), baseLayer, DrainSurgeColor);
        }

        foreach (var (uid, color) in _drainSurgeOriginalColors)
        {
            if (HasComp<XenoDrainSurgeComponent>(uid))
                continue;

            if (!TerminatingOrDeleted(uid) &&
                TryComp(uid, out SpriteComponent? sprite))
            {
                _sprite.SetColor((uid, sprite), color.Sprite);

                if (color.BaseLayer != null &&
                    _sprite.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var baseLayer, false))
                {
                    _sprite.LayerSetColor((uid, sprite), baseLayer, color.BaseLayer.Value);
                }
            }

            _remove.Add(uid);
        }

        foreach (var uid in _remove)
        {
            _drainSurgeOriginalColors.Remove(uid);
        }

        _remove.Clear();
    }

    private DrainSurgeOriginalColor GetOriginalColors(EntityUid uid, SpriteComponent sprite)
    {
        Color? baseLayerColor = null;
        if (_sprite.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var baseLayer, false))
            baseLayerColor = sprite.AllLayers.ElementAt(baseLayer).Color;

        return new DrainSurgeOriginalColor(sprite.Color, baseLayerColor);
    }

    private readonly record struct DrainSurgeOriginalColor(Color Sprite, Color? BaseLayer);
}
