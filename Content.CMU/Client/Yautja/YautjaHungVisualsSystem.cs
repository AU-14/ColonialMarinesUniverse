using System.Numerics;
using Content.Client.Rotation;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.CMU14.Yautja;

// cmss13 get_hung(): flipped upside down, lifted 9 pixels and swinging side to side forever
public sealed partial class YautjaHungVisualsSystem : EntitySystem
{
    private const string SwingKey = "yautja-hung-swing";
    private const string RotateKey = "rotate";
    private static readonly float Lift = 9 / 32f;
    private static readonly TimeSpan SwingHalf = TimeSpan.FromSeconds(3);

    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHungComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaHungComponent, ComponentShutdown>(OnShutdown);
        // the corpse lying-down rotation runs on every appearance change, flip it back after
        SubscribeLocalEvent<YautjaHungComponent, AppearanceChangeEvent>(OnAppearanceChange, after: [typeof(RotationVisualizerSystem)]);
        SubscribeLocalEvent<YautjaHungComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnStartup(Entity<YautjaHungComponent> ent, ref ComponentStartup args)
    {
        Flip(ent);
        PlaySwing(ent);
    }

    private void OnShutdown(Entity<YautjaHungComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent) || !TryComp(ent, out SpriteComponent? sprite))
            return;

        if (TryComp(ent, out AnimationPlayerComponent? player))
            _animation.Stop((ent, player), SwingKey);

        _sprite.SetOffset((ent, sprite), Vector2.Zero);
        _sprite.SetRotation((ent, sprite), Angle.Zero);
        // hands the rotation back to the normal lying/standing visuals
        if (TryComp(ent, out AppearanceComponent? appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    private void OnAppearanceChange(Entity<YautjaHungComponent> ent, ref AppearanceChangeEvent args)
    {
        Flip(ent);
    }

    private void OnAnimationCompleted(Entity<YautjaHungComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key == SwingKey && args.Finished)
            PlaySwing(ent);
    }

    private void Flip(Entity<YautjaHungComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        if (TryComp(ent, out AnimationPlayerComponent? player) && _animation.HasRunningAnimation(player, RotateKey))
            _animation.Stop((ent, player), RotateKey);

        _sprite.SetRotation((ent, sprite), Angle.FromDegrees(180));
    }

    private void PlaySwing(Entity<YautjaHungComponent> ent)
    {
        if (!HasComp<SpriteComponent>(ent))
            return;

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_animation.HasRunningAnimation(player, SwingKey))
            return;

        var left = new Vector2(-ent.Comp.Swing, Lift);
        var right = new Vector2(ent.Comp.Swing, Lift);
        var half = (float) SwingHalf.TotalSeconds;
        _animation.Play((ent, player), new Animation
        {
            Length = SwingHalf * 2,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(left, 0),
                        new AnimationTrackProperty.KeyFrame(right, half),
                        new AnimationTrackProperty.KeyFrame(left, half),
                    },
                },
            },
        }, SwingKey);
    }
}
