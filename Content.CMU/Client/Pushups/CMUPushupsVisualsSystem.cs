using System.Numerics;
using Content.Shared.CMU14.Gym;
using Content.Shared.CMU14.Pushups;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Pushups;

public sealed partial class CMUPushupsVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string OverlayKey = "cmu-exercise-overlay";

    private readonly Dictionary<EntityUid, Vector2> _baseOffsets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPushupsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUPushupsComponent> ent, ref ComponentShutdown args)
    {
        Restore(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPushupsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pushups, out var sprite))
        {
            if (!pushups.Active)
            {
                Restore(uid);
                continue;
            }

            UpdateOverlay((uid, sprite), pushups);

            if (!_baseOffsets.TryGetValue(uid, out var baseOffset))
            {
                baseOffset = sprite.Offset;
                _baseOffsets[uid] = baseOffset;
            }

            var duration = pushups.RepDuration.TotalSeconds;
            var phase = duration <= 0 ? 0 : Math.Clamp((now - pushups.RepStart).TotalSeconds / duration, 0, 1);
            float lift;
            if (MachineMotion(pushups.Exercise) is { } motion)
            {
                // One rep runs the machine's animation out and back, and the body moves frame by frame with it.
                var frame = (int) Math.Round((1 - Math.Abs(2 * phase - 1)) * (motion.Length - 1));
                SyncMachine(pushups.Equipment, frame);
                lift = motion[frame] / 32f;
                SyncOverlay((uid, sprite), frame, lift);
            }
            else
            {
                var height = pushups.Exercise switch
                {
                    CMUExercise.Pushups => 0.08f,
                    CMUExercise.KneePushups => 0.06f,
                    CMUExercise.Situps => 0.04f,
                    CMUExercise.Running => 0.03f,
                    _ => 0f,
                };

                // A runner bobs twice a second, once for each step.
                lift = pushups.Exercise == CMUExercise.Running
                    ? (float) Math.Abs(Math.Sin(phase * 2 * Math.PI)) * height
                    : (float) Math.Sin(phase * Math.PI) * height;
            }

            _sprite.SetOffset((uid, sprite), baseOffset + new Vector2(0, lift));
        }
    }

    /// <summary>
    ///     How far the body moves, in pixels, on each frame of a machine's animation: down as the stack lifter's
    ///     handles are pulled down, and up a touch as the bar is pressed.
    /// </summary>
    private static int[]? MachineMotion(CMUExercise exercise)
    {
        return exercise switch
        {
            CMUExercise.StackLifter => StackLifterMotion,
            CMUExercise.BenchPress => BenchPressMotion,
            _ => null,
        };
    }

    private static readonly int[] StackLifterMotion = { 0, -1, -3, -4 };
    private static readonly int[] BenchPressMotion = { 0, 0, 1, 1 };
    private const float FrameTime = 0.2f;

    private void SyncMachine(EntityUid? machine, int frame)
    {
        if (!TryComp<SpriteComponent>(machine, out var sprite) ||
            !_sprite.LayerMapTryGet((machine.Value, sprite), CMUExerciseMachineVisuals.InUse, out var layer, false))
        {
            return;
        }

        _sprite.LayerSetAutoAnimated((machine.Value, sprite), layer, false);
        _sprite.LayerSetAnimationTime((machine.Value, sprite), layer, frame * FrameTime + 0.01f);
    }

    /// <summary>Steps the barbell with the machine, holding it still against the body's own movement.</summary>
    private void SyncOverlay(Entity<SpriteComponent> ent, int frame, float lift)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), OverlayKey, out var layer, false))
            return;

        _sprite.LayerSetAutoAnimated(ent.AsNullable(), layer, false);
        _sprite.LayerSetAnimationTime(ent.AsNullable(), layer, frame * FrameTime + 0.01f);
        _sprite.LayerSetOffset(ent.AsNullable(), layer, new Vector2(0, -lift));
    }

    /// <summary>Draws the machine's overlay, like a barbell, on the person using it so it sits over them.</summary>
    private void UpdateOverlay(Entity<SpriteComponent> ent, CMUPushupsComponent pushups)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), OverlayKey, out _, false) ||
            !TryComp<CMUExerciseMachineComponent>(pushups.Equipment, out var machine) ||
            machine.UserOverlayState == null ||
            !TryComp<SpriteComponent>(pushups.Equipment, out var machineSprite) ||
            machineSprite.BaseRSI is not { } rsi)
        {
            return;
        }

        var layer = _sprite.AddLayer(ent.AsNullable(), new SpriteSpecifier.Rsi(rsi.Path, machine.UserOverlayState));
        _sprite.LayerMapSet(ent.AsNullable(), OverlayKey, layer);
    }

    private void Restore(EntityUid uid)
    {
        if (TryComp<SpriteComponent>(uid, out var overlaySprite))
            _sprite.RemoveLayer((uid, overlaySprite), OverlayKey, false);

        if (!_baseOffsets.Remove(uid, out var baseOffset))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetOffset((uid, sprite), baseOffset);
    }
}
