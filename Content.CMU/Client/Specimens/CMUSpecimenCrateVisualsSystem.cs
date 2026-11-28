using Content.Shared.CMU14.Specimens;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.CMU14.Specimens;

/// <summary>
///     Plays the specimen crate's lid opening and closing. Built on the AEGIS secure crate's opening
///     animation; closing plays the same frames in reverse.
/// </summary>
public sealed partial class CMUSpecimenCrateVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string AnimationKey = "cmu_specimen_crate_lid";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSpecimenCrateComponent, ComponentStartup>((ent, ref _) => UpdateVisuals(ent));
        SubscribeLocalEvent<CMUSpecimenCrateComponent, AfterAutoHandleStateEvent>((ent, ref _) => UpdateVisuals(ent));
    }

    private void UpdateVisuals(Entity<CMUSpecimenCrateComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((ent, sprite), CMUSpecimenCrateLayers.Base, out var layer, true))
        {
            return;
        }

        switch (ent.Comp.State)
        {
            case CMUSpecimenCrateState.Open:
                StopLid(ent);
                _sprite.LayerSetRsiState((ent, sprite), layer, "open");
                break;
            case CMUSpecimenCrateState.Closed:
                StopLid(ent);
                _sprite.LayerSetRsiState((ent, sprite), layer, "closed");
                break;
            case CMUSpecimenCrateState.Opening:
                PlayLid(ent, "opening", "open");
                break;
            case CMUSpecimenCrateState.Closing:
                PlayLid(ent, "closing", "closed");
                break;
        }
    }

    private void PlayLid(Entity<CMUSpecimenCrateComponent> ent, string moving, string end)
    {
        StopLid(ent);
        var length = (float) ent.Comp.MoveTime.TotalSeconds;
        _animation.Play(ent, new Animation
        {
            Length = ent.Comp.MoveTime,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = CMUSpecimenCrateLayers.Base,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(moving, 0f),
                        new AnimationTrackSpriteFlick.KeyFrame(end, length),
                    },
                },
            },
        }, AnimationKey);
    }

    private void StopLid(EntityUid uid)
    {
        if (TryComp<AnimationPlayerComponent>(uid, out var player) && _animation.HasRunningAnimation(uid, player, AnimationKey))
            _animation.Stop(uid, player, AnimationKey);
    }
}
