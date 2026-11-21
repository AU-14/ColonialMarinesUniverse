using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Automation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.Automation;

/// <summary>
/// CMU14 (ECS-04): draws a harvester or planter arm with the inserter's rig (<see cref="CMUArmRigSystem"/>) and plays
/// the reach the server records on <see cref="CMUTrayArmComponent"/>: out to the tray it worked, a hold, and back to
/// rest. The base stays upright; the whole rig is mirrored when the arm works the left side of the screen, because
/// the West frame it is cut from reaches to the right. Trays further than the links reach are reached by telescoping
/// the forearm along a rod. Cosmetic only.
/// </summary>
public sealed class CMUTrayArmVisualsSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private CMUArmRigSystem _rig = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>The forearm cable's span in the drawn pose; a longer, telescoped span keeps this one's sag.</summary>
    private const float ForearmCableSpan = 0.55f;

    /// <summary>The rod starts this far out from the elbow joint and runs this far under the pushed-out forearm part,
    /// whose pixels begin 5.5 px past its pivot.</summary>
    private const float RodInset = 2f / 32f;
    private const float RodOverlap = 6f / 32f;

    private Texture? _rodTexture;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUTrayArmComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arm, out var sprite, out var xform))
        {
            var meta = MetaData(uid);
            if ((meta.Flags & MetaDataFlags.Detached) != 0)
                continue;

            // Grid space to the drawn (no-rotation) frame, as the inserter converts its transfer rotation.
            var frame = (xform.GridUid is { } grid ? _transform.GetWorldRotation(grid) : Angle.Zero) + _eye.CurrentEye.Rotation;
            // The server works East for an East facing and West for anything else.
            var facing = xform.LocalRotation.GetCardinalDir() == Direction.East ? Vector2.UnitX : -Vector2.UnitX;
            var mirrored = frame.RotateVec(facing).X < 0f;

            // Rest unless a reach was recorded (a fresh component has ReachStartedAt zero) and the arm still stands where it worked.
            var progress = 1f;
            if (xform.Anchored && arm.ReachStartedAt != TimeSpan.Zero && arm.ReachTarget != Vector2i.Zero)
            {
                var now = _timing.CurTime - _metadata.GetPauseTime(uid, meta);
                progress = (float) (now - arm.ReachStartedAt).TotalSeconds / Math.Max(0.01f, arm.ReachDuration);
            }

            // The rig draws the shoulder at the sprite origin, the centre of the arm's tile, so the tile-centre to
            // tile-centre offset is the shoulder-to-tray offset.
            var target = frame.RotateVec(new Vector2(arm.ReachTarget.X, arm.ReachTarget.Y));
            var pose = CMUTrayArmAnimation.GetPose(CMUTrayArmAnimation.Mirror(target, mirrored), progress);
            DrawArm((uid, sprite), arm, pose, mirrored, progress);
        }
    }

    private void DrawArm(Entity<SpriteComponent> sprite, CMUTrayArmComponent arm, CMUTrayArmPose pose, bool mirrored, float progress)
    {
        var visuals = EnsureComp<CMUTrayArmVisualsComponent>(sprite);
        if (visuals.Layers.Count == 0)
        {
            _rig.EnsureLayers(sprite, visuals.Layers, visuals.Cables);
            // The rod passes over the upper arm and under the forearm it pushes out; the load is drawn on top of everything.
            var layerCount = sprite.Comp.AllLayers.Count();
            visuals.Rod = _sprite.AddBlankLayer(sprite, layerCount - visuals.Layers.Count + _rig.ForearmPart);
            _sprite.LayerSetTexture(visuals.Rod, GetRodTexture());
            _sprite.LayerSetAutoAnimated(visuals.Rod, false);
            _sprite.LayerSetVisible(visuals.Rod, false);
            visuals.Item = _sprite.AddBlankLayer(sprite);
            _sprite.LayerSetAutoAnimated(visuals.Item, false);
            _sprite.LayerSetVisible(visuals.Item, false);
        }

        // The cables are laid out in the work frame and mirrored with the rest; a mirrored curve bulges the other way,
        // so the slack's sign flips too.
        var sign = mirrored ? -1f : 1f;
        var baseCable = CMUInserterAnimation.PixelOffset(new Vector2(5, 42), CMUInserterAnimation.ShoulderPixels);
        var upperCable = pose.Elbow + pose.Shoulder.RotateVec(CMUInserterAnimation.PixelOffset(new Vector2(20, 30), CMUInserterAnimation.ElbowPixels));
        _rig.DrawCable(visuals.Cables, 0,
            CMUTrayArmAnimation.Mirror(baseCable, mirrored),
            CMUTrayArmAnimation.Mirror(upperCable, mirrored),
            0.65f * sign);
        var elbowCable = pose.Elbow + pose.Shoulder.RotateVec(CMUInserterAnimation.PixelOffset(new Vector2(23, 39), CMUInserterAnimation.ElbowPixels));
        var forearmCable = pose.ForearmPivot + pose.Forearm.RotateVec(CMUInserterAnimation.PixelOffset(new Vector2(40, 37), CMUInserterAnimation.ElbowPixels));
        // Stretched along the rod, the cable keeps the sag it has at its drawn length instead of looping wide.
        var span = (forearmCable - elbowCable).Length();
        var slack = -0.65f * Math.Min(1f, ForearmCableSpan / Math.Max(span, 0.001f));
        _rig.DrawCable(visuals.Cables, CMUArmRigSystem.CableSegments,
            CMUTrayArmAnimation.Mirror(elbowCable, mirrored),
            CMUTrayArmAnimation.Mirror(forearmCable, mirrored),
            slack * sign);

        _rig.PlaceParts(visuals.Layers, pose.Shoulder, pose.Forearm, pose.ForearmPivot, mirrored);
        DrawRod(visuals.Rod!, pose, mirrored);
        DrawItem(visuals, arm, pose, mirrored, progress);
    }

    private void DrawRod(SpriteComponent.Layer rod, CMUTrayArmPose pose, bool mirrored)
    {
        var telescoped = pose.Telescope > 0.5f / 32f;
        _sprite.LayerSetVisible(rod, telescoped);
        if (!telescoped)
            return;

        var direction = (pose.ForearmPivot - pose.Elbow) / pose.Telescope;
        var from = CMUTrayArmAnimation.Mirror(pose.Elbow + direction * RodInset, mirrored);
        var to = CMUTrayArmAnimation.Mirror(pose.ForearmPivot + direction * RodOverlap, mirrored);
        var delta = to - from;
        _sprite.LayerSetOffset(rod, (from + to) * 0.5f);
        _sprite.LayerSetRotation(rod, new Angle(delta));
        // The texture is one pixel long and three thick.
        _sprite.LayerSetScale(rod, new Vector2(delta.Length() * 32f, 1f));
    }

    private void DrawItem(CMUTrayArmVisualsComponent visuals, CMUTrayArmComponent arm, CMUTrayArmPose pose, bool mirrored, float progress)
    {
        var item = visuals.Item!;
        if (arm.ReachCarries is not { } carried || !CMUTrayArmAnimation.ShowsCarried(progress, arm.ReachCarriesOut))
        {
            _sprite.LayerSetVisible(item, false);
            return;
        }

        if (visuals.ItemShown != carried)
        {
            _sprite.LayerSetTexture(item, _sprite.GetPrototypeIcon(carried).Default);
            visuals.ItemShown = carried;
        }
        _sprite.LayerSetVisible(item, true);
        _sprite.LayerSetOffset(item, CMUTrayArmAnimation.Mirror(pose.Claw, mirrored));
        _sprite.LayerSetRotation(item, Angle.Zero);
        _sprite.LayerSetScale(item, new Vector2(0.25f, 0.25f));
    }

    private Texture GetRodTexture()
    {
        if (_rodTexture != null)
            return _rodTexture;

        using var rod = new Image<Rgba32>(1, 3);
        var pixels = rod.GetPixelSpan();
        pixels[0] = new Rgba32(150, 154, 160, 255);
        pixels[1] = new Rgba32(104, 108, 115, 255);
        pixels[2] = new Rgba32(62, 64, 70, 255);
        return _rodTexture = Texture.LoadFromImage(rod, "CMU tray arm rod");
    }
}
