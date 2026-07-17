using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaChainGauntletAnimationSystem : EntitySystem
{
    private const string ExecutionAnimationKey = "cmu-yautja-chain-gauntlet-execution";

    [Dependency] private AnimationPlayerSystem _animation = default!;

    public override void Initialize()
    {
        SubscribeAllEvent<YautjaChainGauntletExecutionAnimationEvent>(OnExecutionAnimation);
    }

    private void OnExecutionAnimation(YautjaChainGauntletExecutionAnimationEvent ev)
    {
        if (!TryGetEntity(ev.Target, out var target) ||
            !TryComp<SpriteComponent>(target, out var sprite))
        {
            return;
        }

        var animation = GetExecutionAnimation(sprite.Offset, ev.LiftHeight, ev.LiftDuration, ev.DropDuration);
        _animation.Stop(target.Value, ExecutionAnimationKey);
        _animation.Play(target.Value, animation, ExecutionAnimationKey);
    }

    private static Animation GetExecutionAnimation(Vector2 startOffset, float liftHeight, TimeSpan liftDuration, TimeSpan dropDuration)
    {
        var liftSeconds = (float) liftDuration.TotalSeconds;
        var totalSeconds = (float) (liftDuration + dropDuration).TotalSeconds;
        var liftedOffset = startOffset + new Vector2(0f, liftHeight);

        return new Animation
        {
            Length = liftDuration + dropDuration,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new KeyFrame(startOffset, 0f),
                        new KeyFrame(liftedOffset, liftSeconds),
                        new KeyFrame(startOffset, totalSeconds),
                    },
                },
            },
        };
    }
}
