using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaChainGauntletAnimationSystem : EntitySystem
{
    private const string ExecutionAnimationKey = "cmu-yautja-chain-gauntlet-execution";

    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    // offset the mob had before the lift, put back when the animation ends or gets cut off
    private readonly Dictionary<EntityUid, Vector2> _baseOffsets = new();

    public override void Initialize()
    {
        SubscribeAllEvent<YautjaChainGauntletExecutionAnimationEvent>(OnExecutionAnimation);
        SubscribeLocalEvent<SpriteComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _baseOffsets.Clear();
    }

    private void OnExecutionAnimation(YautjaChainGauntletExecutionAnimationEvent ev)
    {
        if (!TryGetEntity(ev.Target, out var target) ||
            !TryComp<SpriteComponent>(target, out var sprite))
        {
            return;
        }

        // stopping a running one snaps the sprite back to its base through OnAnimationCompleted first
        _animation.Stop(target.Value, ExecutionAnimationKey);

        // corpses deleted mid-lift never fire the completed event
        foreach (var stale in _baseOffsets.Keys.Where(uid => Deleted(uid)).ToList())
        {
            _baseOffsets.Remove(stale);
        }

        var baseOffset = sprite.Offset;
        _baseOffsets[target.Value] = baseOffset;

        var animation = GetExecutionAnimation(baseOffset, ev.LiftHeight, ev.LiftDuration, ev.DropDuration);
        _animation.Play(target.Value, animation, ExecutionAnimationKey);
    }

    private void OnAnimationCompleted(Entity<SpriteComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != ExecutionAnimationKey || !_baseOffsets.Remove(ent.Owner, out var baseOffset))
            return;

        // stopped early (pvs, a second execution) leaves the sprite wherever the track was, snap it home
        _sprite.SetOffset(ent.AsNullable(), baseOffset);
    }

    private static Animation GetExecutionAnimation(Vector2 startOffset, float liftHeight, TimeSpan liftDuration, TimeSpan dropDuration)
    {
        var liftedOffset = startOffset + new Vector2(0f, liftHeight);

        // keyframe times are relative to the previous keyframe. the drop frame used the total time, so the
        // animation ended halfway down and the corpse stayed floating above its tile
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
                        new KeyFrame(liftedOffset, (float) liftDuration.TotalSeconds),
                        new KeyFrame(startOffset, (float) dropDuration.TotalSeconds),
                    },
                },
            },
        };
    }
}
