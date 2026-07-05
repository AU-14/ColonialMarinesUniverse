using System.Numerics;
using Content.Shared._CMU14.DroneOperator;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._CMU14.DroneOperator;

public sealed partial class CMUDroneOperatorVisualSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string TransferShakeKey = "cmu-drone-transfer-shake";
    private const float ShakeAmplitude = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUDroneAndroidShakeEvent>(OnDroneAndroidShake);
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
                        new AnimationTrackProperty.KeyFrame(start + new Vector2(ShakeAmplitude, 0f), duration * 0.25f),
                        new AnimationTrackProperty.KeyFrame(start + new Vector2(-ShakeAmplitude, 0f), duration * 0.5f),
                        new AnimationTrackProperty.KeyFrame(start + new Vector2(ShakeAmplitude * 0.5f, 0f), duration * 0.75f),
                        new AnimationTrackProperty.KeyFrame(start, duration),
                    }
                }
            }
        };

        _animation.Play((drone, player), animation, TransferShakeKey);
    }
}
