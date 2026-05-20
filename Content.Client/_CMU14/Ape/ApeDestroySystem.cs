using Content.Shared._CMU14.Ape;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using System.Numerics;

namespace Content.Client._CMU14.Ape;

public sealed partial class ApeDestroySystem : SharedApeDestroySystem
{
    [Dependency] private AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float JumpHeight = 10;

    private const string LeapingAnimationKey = "ape-leap-animation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<ApeDestroyLeapStartEvent>(OnApeLeapStart);
    }

    public Animation LeapAnimation(ApeDestroyComponent destroy, Vector2 leapOffset)
    {
        var midpoint = (leapOffset / 2);
        var opposite = -midpoint;

        midpoint += new Vector2(0, JumpHeight);
        opposite += new Vector2(0, JumpHeight);

        var midtime = (float)(destroy.CrashTime.TotalSeconds / 2f);

        return new Animation
        {
            Length = destroy.CrashTime,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(midpoint, midtime),
                        new AnimationTrackProperty.KeyFrame(opposite, 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, midtime),
                    }
                }
            }
        };
    }

    private void OnApeLeapStart(ApeDestroyLeapStartEvent ev)
    {
        if (!TryGetEntity(ev.King, out var ape) || !TryComp<ApeDestroyComponent>(ape, out var destroy))
            return;

        if (!TryComp<SpriteComponent>(ape, out var sprite) || TerminatingOrDeleted(ape))
            return;

        if (!TryComp<AnimationPlayerComponent>(ape, out var player))
            return;

        if (_animPlayer.HasRunningAnimation(player, LeapingAnimationKey))
            return;

        _animPlayer.Play(ape.Value, LeapAnimation(destroy, ev.LeapOffset), LeapingAnimationKey);
    }

    protected override void OnLeapingRemove(Entity<ApeDestroyLeapingComponent> ape, ref ComponentRemove args)
    {
        base.OnLeapingRemove(ape, ref args);

        if (!TryComp<SpriteComponent>(ape, out var sprite) || TerminatingOrDeleted(ape))
            return;

        if (TryComp(ape, out AnimationPlayerComponent? animation))
            _animPlayer.Stop((ape, animation), LeapingAnimationKey);

        _sprite.SetOffset((ape.Owner, sprite), Vector2.Zero);
    }
}

