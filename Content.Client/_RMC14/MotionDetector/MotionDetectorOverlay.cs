using System.Numerics;
using Content.Shared._RMC14.MotionDetector;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.MotionDetector;

public sealed partial class MotionDetectorOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private TimeSpan _last;
    private readonly List<(Vector2 Pos, bool QueenEye)> _blips = new();

    private readonly MotionDetectorOverlaySystem _motionDetector;
    private readonly SpriteSystem _sprite;

    // CMU14 Begin: descriptors are immutable; frame lookup still follows animation/resource reloads.
    private static readonly SpriteSpecifier.Rsi CMUBlip = new(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"), "detector_blip");
    private static readonly SpriteSpecifier.Rsi CMUQueenEye = new(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"), "queen_eye_blip");
    // CMU14 End

    public MotionDetectorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _motionDetector = _entity.System<MotionDetectorOverlaySystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var frame = _sprite.GetFrame(CMUBlip, _timing.CurTime); // CMU14
        var queenEyeFrame = _sprite.GetFrame(CMUQueenEye, _timing.CurTime); // CMU14
        _motionDetector.DrawBlips<MotionDetectorComponent>(args.WorldHandle, ref _last, _blips, frame, queenEyeFrame);
    }
}
