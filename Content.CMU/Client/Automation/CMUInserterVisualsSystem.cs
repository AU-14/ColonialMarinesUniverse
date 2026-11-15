using System.Numerics;
using Content.Client.Clickable;
using Content.Shared.CMU14.Automation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Containers;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.Automation;

/// <summary>Pivots the original upper arm and forearm at their joints, carrying the real item in the claw.</summary>
public sealed partial class CMUInserterVisualsSystem : EntitySystem
{
    [Dependency] private IClickMapManager _clickMaps = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private ArmPart[]? _parts;
    private Texture? _cableTexture;
    private const int CableSegments = 12;
    private readonly Dictionary<EntityUid, CarriedSprite> _carried = new();
    private readonly HashSet<EntityUid> _current = new();
    private readonly List<EntityUid> _released = new();

    public override void FrameUpdate(float frameTime)
    {
        _current.Clear();
        var query = EntityQueryEnumerator<CMUInserterComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arm, out var sprite, out var xform))
        {
            var meta = MetaData(uid);
            if ((meta.Flags & MetaDataFlags.Detached) != 0)
                continue;

            EntityUid? held = null;
            if (_containers.TryGetContainer(uid, CMUInserterComponent.ClawContainerId, out var claw)
                && claw is ContainerSlot slot)
                held = slot.ContainedEntity;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, meta);
            var progress = arm.Enabled
                ? (float) (now - arm.PhaseStartedAt).TotalSeconds / Math.Max(0.01f, arm.PhaseDuration)
                : arm.PausedProgress;
            var pose = CMUInserterAnimation.GetPose(arm.Phase, progress);
            var rotation = arm.Phase == CMUInserterPhase.Idle
                ? _transform.GetWorldRotation(uid)
                : (xform.GridUid is { } grid ? _transform.GetWorldRotation(grid) : Angle.Zero) + arm.TransferRotation;
            var screenRotation = rotation + _eye.CurrentEye.Rotation;
            DrawArm(uid, sprite, screenRotation, pose);
            if (held is { } item && TryComp(item, out SpriteComponent? itemSprite))
                DrawItem(item, itemSprite, sprite.DrawDepth, screenRotation.RotateVec(pose.Item));
        }

        // Releasing an item restores its full sprite, including when the arm leaves PVS.
        _released.Clear();
        foreach (var (uid, original) in _carried)
        {
            if (_current.Contains(uid))
                continue;
            if (TryComp(uid, out SpriteComponent? sprite))
            {
                sprite.NoRotation = original.NoRotation;
                _sprite.SetOffset((uid, sprite), original.Offset);
                _sprite.SetScale((uid, sprite), original.Scale);
                _sprite.SetRotation((uid, sprite), original.Rotation);
                _sprite.SetDrawDepth((uid, sprite), original.DrawDepth);
            }
            _released.Add(uid);
        }
        foreach (var uid in _released)
            _carried.Remove(uid);
    }

    private void DrawItem(EntityUid item, SpriteComponent sprite, int armDepth, Vector2 offset)
    {
        if (!_carried.TryGetValue(item, out var original))
        {
            original = new CarriedSprite(sprite.Offset, sprite.Scale, sprite.Rotation, sprite.DrawDepth, sprite.NoRotation);
            _carried.Add(item, original);
        }
        _current.Add(item);
        sprite.NoRotation = true;
        _sprite.SetRotation((item, sprite), Angle.Zero);
        _sprite.SetOffset((item, sprite), offset);
        _sprite.SetScale((item, sprite), original.Scale * 0.25f);
        // The load stays visible as the folded claw crosses in front of the pedestal.
        _sprite.SetDrawDepth((item, sprite), armDepth + 1);
    }

    private void DrawArm(EntityUid uid, SpriteComponent sprite, Angle rotation, CMUInserterPose pose)
    {
        _parts ??= LoadParts();
        var visuals = EnsureComp<CMUInserterVisualsComponent>(uid);
        if (visuals.Layers.Count == 0)
        {
            // The regions preserve the original pixels and proportions; only the two joint angles change.
            _sprite.TryGetLayer((uid, sprite), 0, out var body, false);
            visuals.Cables.Add(body!);
            for (var i = 1; i < CableSegments * 2; i++)
                visuals.Cables.Add(_sprite.AddBlankLayer((uid, sprite)));
            foreach (var cable in visuals.Cables)
            {
                _sprite.LayerSetTexture(cable, _cableTexture!);
                _sprite.LayerSetAutoAnimated(cable, false);
            }
            for (var i = 0; i < _parts.Length; i++)
                visuals.Layers.Add(_sprite.AddBlankLayer((uid, sprite)));
            for (var i = 0; i < _parts.Length; i++)
            {
                _sprite.LayerSetTexture(visuals.Layers[i], _parts[i].Texture);
                _sprite.LayerSetAutoAnimated(visuals.Layers[i], false);
            }
        }

        var shoulderRotation = rotation + pose.Shoulder;
        var forearmRotation = rotation + pose.Forearm;
        var elbowPosition = rotation.RotateVec(pose.Elbow);
        DrawCable(visuals, 0,
            CMUInserterAnimation.PixelOffset(new Vector2(5, 42), CMUInserterAnimation.ShoulderPixels),
            elbowPosition + shoulderRotation.RotateVec(CMUInserterAnimation.PixelOffset(new Vector2(20, 30), CMUInserterAnimation.ElbowPixels)),
            0.65f);
        DrawCable(visuals, CableSegments,
            elbowPosition + shoulderRotation.RotateVec(CMUInserterAnimation.PixelOffset(new Vector2(23, 39), CMUInserterAnimation.ElbowPixels)),
            elbowPosition + forearmRotation.RotateVec(CMUInserterAnimation.PixelOffset(new Vector2(40, 37), CMUInserterAnimation.ElbowPixels)),
            -0.65f);

        var layers = visuals.Layers;
        for (var i = 0; i < _parts.Length; i++)
        {
            var part = _parts[i];
            var angle = part.Joint switch
            {
                ArmJoint.Shoulder => rotation + pose.Shoulder,
                ArmJoint.Elbow => rotation + pose.Forearm,
                _ => Angle.Zero,
            };
            var pivot = part.Joint == ArmJoint.Elbow ? rotation.RotateVec(pose.Elbow) : Vector2.Zero;
            _sprite.LayerSetOffset(layers[i], pivot + angle.RotateVec(part.Offset));
            _sprite.LayerSetRotation(layers[i], angle);
        }
    }

    private void DrawCable(CMUInserterVisualsComponent visuals, int first, Vector2 start, Vector2 end, float slack)
    {
        var span = end - start;
        var control = (start + end) * 0.5f + new Vector2(-span.Y, span.X) * slack;
        var previous = start;
        for (var i = 0; i < CableSegments; i++)
        {
            var t = (i + 1f) / CableSegments;
            var point = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
            var delta = point - previous;
            var layer = visuals.Cables[first + i];
            _sprite.LayerSetOffset(layer, (previous + point) * 0.5f);
            _sprite.LayerSetRotation(layer, new Angle(delta));
            // A slight overlap prevents pixel rounding from opening gaps between the short segments.
            _sprite.LayerSetScale(layer, new Vector2((delta.Length() * 32f + 0.5f) / 2f, 1f));
            previous = point;
        }
    }

    private ArmPart[] LoadParts()
    {
        var state = _sprite.GetState(new SpriteSpecifier.Rsi(new ResPath("CMU14/Structures/factory.rsi"), "factory_roboticarm2"));
        var texture = LoadArmTexture();
        return
        [
            Part(state, texture, new UIBox2i(0, 44, 64, 64), ArmJoint.Fixed),
            Part(state, texture, new UIBox2i(0, 38, 18, 44), ArmJoint.Fixed),
            Part(state, texture, new UIBox2i(0, 0, 30, 38), ArmJoint.Shoulder),
            Part(state, texture, new UIBox2i(18, 38, 30, 44), ArmJoint.Shoulder),
            // The shoulder bearing covers the upper arm's seam; the claw passes in front of the base.
            Part(state, texture, new UIBox2i(5, 38, 18, 50), ArmJoint.Fixed),
            Part(state, texture, new UIBox2i(30, 0, 64, 44), ArmJoint.Elbow),
        ];
    }

    private Texture LoadArmTexture()
    {
        using var stream = _resources.ContentFileRead(new ResPath("/Textures/CMU14/Structures/factory.rsi/factory_roboticarm2.png"));
        using var sheet = Image.Load<Rgba32>(stream);
        using var arm = new Image<Rgba32>(64, 64);
        var source = sheet.GetPixelSpan();
        var pixels = arm.GetPixelSpan();
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            // West's first frame is the first cell of the third row. The source's blue-grey cable
            // pixels are replaced at render time by continuous curves; preserve the motor and neck.
            var pixel = source[(y + 128) * sheet.Width + x];
            var housing = x >= 5 && x < 18 && y >= 38 && y < 54;
            if (pixel.B > pixel.R && !housing)
                pixel.A = 0;
            pixels[y * 64 + x] = pixel;
        }
        using var cable = new Image<Rgba32>(2, 2);
        var wire = cable.GetPixelSpan();
        wire[0] = wire[1] = new Rgba32(55, 57, 66, 255);
        wire[2] = wire[3] = new Rgba32(33, 35, 38, 255);
        _cableTexture = Texture.LoadFromImage(cable, "CMU inserter cable");
        return Texture.LoadFromImage(arm, "CMU inserter arm");
    }

    private ArmPart Part(RSI.State state, Texture texture, UIBox2i bounds, ArmJoint joint)
    {
        var center = ((Vector2) bounds.TopLeft + bounds.BottomRight) * 0.5f;
        var pivot = joint == ArmJoint.Elbow ? CMUInserterAnimation.ElbowPixels : CMUInserterAnimation.ShoulderPixels;
        var part = new AtlasTexture(texture, new UIBox2(bounds.TopLeft, bounds.BottomRight));
        _clickMaps.RegisterRsiRegion(part, state.RSI, state.StateId, RsiDirection.West, bounds);
        return new ArmPart(part, CMUInserterAnimation.PixelOffset(center, pivot), joint);
    }

    private readonly record struct CarriedSprite(Vector2 Offset, Vector2 Scale, Angle Rotation, int DrawDepth, bool NoRotation);

    private enum ArmJoint : byte { Fixed, Shoulder, Elbow }
    private readonly record struct ArmPart(Texture Texture, Vector2 Offset, ArmJoint Joint);
}
