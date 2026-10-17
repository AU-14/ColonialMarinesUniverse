using Content.Shared._RMC14.Xenonids.Egg;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._RMC14.Xenonids.Egg;

public sealed partial class XenoEggVisualizerSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string AnimationKey = "rmc_egg_destroying";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEggComponent, ComponentStartup>(SetVisuals);
        SubscribeLocalEvent<XenoEggComponent, XenoEggStateChangedEvent>(SetVisuals);

        SubscribeLocalEvent<DestroyedXenoEggComponent, ComponentStartup>(OnStartup);
    }

    private void SetVisuals<T>(Entity<XenoEggComponent> ent, ref T args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var expectedSprite = ent.Comp.CurrentSprite;

        if (!_resourceCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / expectedSprite, out var res))
            return;

        var hasBaseLayer = _sprite.LayerMapTryGet((ent.Owner, sprite), XenoEggLayers.Base, out var layer, false);
        SpriteComponent.Layer? baseLayer = null;
        if (hasBaseLayer)
            hasBaseLayer = _sprite.TryGetLayer((ent.Owner, sprite), layer, out baseLayer, false);
        var state = ent.Comp.State switch
        {
            XenoEggState.Item => ent.Comp.ItemState,
            XenoEggState.Growing => ent.Comp.GrowingState,
            XenoEggState.Grown => ent.Comp.GrownState,
            XenoEggState.Opened => ent.Comp.OpenedState,
            XenoEggState.Opening => ent.Comp.OpeningState,
            _ => null
        };

        if (!XenoEggStateResolver.TryResolve(
                state,
                candidate => res.RSI.TryGetState(candidate, out _),
                out var resolvedState))
            return;

        if (sprite.BaseRSI != res.RSI)
        {
            if (hasBaseLayer && baseLayer!.RSI == null && !res.RSI.TryGetState(baseLayer.State, out _))
                _sprite.LayerSetRsi((ent.Owner, sprite), layer, res.RSI, resolvedState);
            else
                _sprite.SetBaseRsi((ent.Owner, sprite), res.RSI);
        }

        if (hasBaseLayer)
            _sprite.LayerSetRsiState((ent.Owner, sprite), layer, resolvedState);
    }

    private void OnStartup(Entity<DestroyedXenoEggComponent> ent, ref ComponentStartup args)
    {
        if (_animation.HasRunningAnimation(ent, AnimationKey))
            return;

        _animation.Play(ent,
           new Animation
           {
               Length = ent.Comp.AnimationTime,
               AnimationTracks =
               {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = ent.Comp.Layer,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.AnimationState, 0f),
                        },
                    },
               },
           },
           AnimationKey);
    }
}
