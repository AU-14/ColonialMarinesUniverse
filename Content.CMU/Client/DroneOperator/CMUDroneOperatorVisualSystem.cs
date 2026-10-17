using System.Numerics;
using Content.Shared.CMU14.DroneOperator;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.DroneOperator;

public sealed partial class CMUDroneOperatorVisualSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string TransferShakeKey = "cmu-drone-transfer-shake";
    private const float ShakeAmplitude = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUDroneAndroidShakeEvent>(OnDroneAndroidShake);
        SubscribeLocalEvent<CMUDroneTransferShakeComponent, AnimationCompletedEvent>(OnShakeCompleted);
    }

    private void OnDroneAndroidShake(CMUDroneAndroidShakeEvent ev)
    {
        if (GetEntity(ev.Drone) is not { Valid: true } drone ||
            TerminatingOrDeleted(drone) ||
            !TryComp<SpriteComponent>(drone, out var sprite))
        {
            return;
        }

        var player = EnsureComp<AnimationPlayerComponent>(drone);
        if (_animation.HasRunningAnimation(player, TransferShakeKey))
            _animation.Stop((drone, player), TransferShakeKey);

        var duration = MathF.Max(0.01f, ev.Duration);
        var start = sprite.Offset;
        EnsureComp<CMUDroneTransferShakeComponent>(drone).OriginalOffset = start;
        // Key times are intervals, so all four steps must fit inside the animation duration.
        var interval = duration * 0.25f;
        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(start, 0f),
                        new AnimationTrackProperty.KeyFrame(start + new Vector2(ShakeAmplitude, 0f), interval),
                        new AnimationTrackProperty.KeyFrame(start + new Vector2(-ShakeAmplitude, 0f), interval),
                        new AnimationTrackProperty.KeyFrame(start + new Vector2(ShakeAmplitude * 0.5f, 0f), interval),
                        new AnimationTrackProperty.KeyFrame(start, interval),
                    }
                }
            }
        };

        _animation.Play((drone, player), animation, TransferShakeKey);
    }

    private void OnShakeCompleted(Entity<CMUDroneTransferShakeComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != TransferShakeKey)
            return;

        // Stopping an animation does not restore its last keyframe. Restore before a new transfer starts.
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetOffset((ent, sprite), ent.Comp.OriginalOffset);

        RemComp<CMUDroneTransferShakeComponent>(ent);
    }
}
