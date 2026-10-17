using System.Numerics;
using Content.Shared._RMC14.MineDetector;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.MineDetector;

public sealed partial class MineDetectorOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private TimeSpan _last;
    private readonly List<(Vector2 Pos, bool QueenEye)> _blips = new();

    private readonly MineDetectorOverlaySystem _mineDetector;
    private readonly SpriteSystem _sprite;

    public MineDetectorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _mineDetector = _entity.System<MineDetectorOverlaySystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var frame = _sprite.GetFrame(new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"), "detector_blip"), _timing.CurTime);
        var queenEyeFrame = _sprite.GetFrame(new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"), "queen_eye_blip"), _timing.CurTime);
        _mineDetector.DrawBlips<MineDetectorComponent>(args.WorldHandle, ref _last, _blips, frame, queenEyeFrame);
    }
}
