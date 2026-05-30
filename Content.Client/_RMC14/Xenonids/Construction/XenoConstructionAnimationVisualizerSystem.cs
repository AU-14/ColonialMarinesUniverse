using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Xenonids.Construction;

public sealed partial class XenoConstructionAnimationVisualizerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<XenoConstructionAnimationStartEvent>(OnAnimateResinBuilding);
    }

    private void OnAnimateResinBuilding(XenoConstructionAnimationStartEvent ev)
    {
        if (!TryGetEntity(ev.Effect, out var eff) ||
        !TryGetEntity(ev.Xeno, out var entity) ||
        !TryComp(eff, out XenoConstructionAnimationComponent? timing))
        {
            return;
        }

        if (!TryComp<SpriteComponent>(eff, out var sprite))
            return;

        var effectUid = eff.Value;
        _sprite.LayerMapTryGet((effectUid, sprite), XenoConstructionVisualLayers.Animation, out var layer, false);

        timing.AnimationTime = ev.BuildTime;
        timing.AnimationTimeFinished = _timing.CurTime + ev.BuildTime;
        if(_sprite.TryGetLayer((effectUid, sprite), layer, out var aLayer, false) && aLayer.ActualState != null)
            timing.TotalFrames = aLayer.ActualState.DelayCount;
    }

    private void Animate(EntityUid uid, SpriteComponent sprite, XenoConstructionVisualLayers layerKey, int frame)
    {
        if (!_sprite.TryGetLayer((uid, sprite), layerKey, out var layer, false))
        {
            return;
        }
        _sprite.LayerSetAutoAnimated(layer, layer.AnimationFrame < frame);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var constructQuery = EntityQueryEnumerator<XenoConstructionAnimationComponent, SpriteComponent>();
        while (constructQuery.MoveNext(out var uid, out var effect, out var sprite))
        {
            double progression = (effect.AnimationTimeFinished - _timing.CurTime) / effect.AnimationTime;
            if (progression < 0)
                progression = 0;
            int expectedFrame = (int) Math.Min(effect.TotalFrames * (1 - progression), effect.TotalFrames - 1);
            Animate(uid, sprite, XenoConstructionVisualLayers.Animation, expectedFrame);
        }

    }
}
