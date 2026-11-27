using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Atmos;

public sealed partial class RMCFlammableSystem : SharedRMCFlammableSystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private Content.Shared.Mobs.Systems.MobStateSystem _mobState = default!; // CMU14
    [Dependency] private SharedTransformSystem _transform = default!; // CMU14

    private const string RollKey = "StopDropRollAnimation";
    private readonly Dictionary<EntityUid, Angle> _preRollRotation = new(); // CMU14
    private static readonly ProtoId<StatusEffectPrototype> KnockdownedKey = "KnockedDown";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<RMCStopDropRollVisualsNetworkEvent>(OnResist);

        SubscribeLocalEvent<RMCStopDropRollVisualsComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RMCStopDropRollVisualsComponent, StatusEffectEndedEvent>(OnStatusEffectEnded);
        SubscribeLocalEvent<RMCStopDropRollVisualsComponent, StoodEvent>(OnStood);
    }

    private void OnResist(RMCStopDropRollVisualsNetworkEvent ev)
    {
        if (!TryGetEntity(ev.User, out var user) || !HasComp<RMCStopDropRollVisualsComponent>(user))
            return;

        // CMU14: the roll event can land after crit, and nothing would stop it then, leaving the body upright (BUG-1502)
        if (!_mobState.IsAlive(user.Value))
            return;

        if (_animation.HasRunningAnimation(user.Value, RollKey))
            return;

        var rollAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(5),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(TransformComponent),
                    Property = nameof(TransformComponent.LocalRotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.25f),
                    }
                }
            }
        };

        _preRollRotation[user.Value] = Transform(user.Value).LocalRotation; // CMU14
        _animation.Play(user.Value, rollAnimation, RollKey);
    }

    // CMU14: stopping the spin left the body at whatever angle it had reached, so a crit body could render upright (BUG-1502)
    private void StopRoll(EntityUid uid)
    {
        if (!_animation.HasRunningAnimation(uid, RollKey))
            return;

        _animation.Stop(uid, RollKey);
        if (_preRollRotation.Remove(uid, out var rotation))
            _transform.SetLocalRotation(uid, rotation);
    }

    private void OnMobStateChanged(Entity<RMCStopDropRollVisualsComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        StopRoll(ent.Owner); // CMU14
    }

    private void OnStatusEffectEnded(Entity<RMCStopDropRollVisualsComponent> ent, ref StatusEffectEndedEvent args)
    {
        if (args.Key != KnockdownedKey)
            return;

        StopRoll(ent.Owner); // CMU14
    }

    private void OnStood(Entity<RMCStopDropRollVisualsComponent> ent, ref StoodEvent args)
    {
        StopRoll(ent.Owner); // CMU14
    }
}
