using System.Numerics;
using Content.Client.UserInterface.Systems.Viewport;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Visor;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Visor;

/// <summary>
/// Draws the motion tracker blips a squad optic picks up from squadmates' trackers, the same way a held tracker draws them.
/// </summary>
public sealed class CMUSquadMotionLinkOverlay : Overlay
{
    private static readonly SpriteSpecifier.Rsi Blip = new(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"), "detector_blip");
    private static readonly SpriteSpecifier.Rsi QueenEyeBlip = new(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"), "queen_eye_blip");

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CMUSquadMotionLinkOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            !_entity.TryGetComponent(player, out CMUSquadMotionLinkComponent? link) ||
            !_entity.TryGetComponent(player, out CMUSquadVisorWearerComponent? wearer) ||
            !wearer.MotionLinkEnabled)
        {
            return;
        }

        var duration = link.ScanDuration;
        if (_net.ServerChannel is { } channel)
            duration += TimeSpan.FromMilliseconds(channel.Ping / 2f);

        var time = _timing.CurTime;
        if (time > link.LastScan + duration)
            return;

        var playerCoords = _transform.GetMapCoordinates(player);

        // Blips past the edge of the screen sit on the edge, like a held tracker's.
        float vpHeight = ViewportUIController.ViewportHeight;
        float vpWidth = _config.GetCVar(CCVars.ViewportWidth);
        var eye = _eye.CurrentEye;
        if (eye.Rotation.GetCardinalDir() is Direction.East or Direction.West)
            (vpWidth, vpHeight) = (vpHeight, vpWidth);

        vpWidth *= eye.Zoom.X;
        vpHeight *= eye.Zoom.Y;

        var blip = _sprite.GetFrame(Blip, time);
        var queenEye = _sprite.GetFrame(QueenEyeBlip, time);
        foreach (var detected in link.Blips)
        {
            if (detected.Coordinates.MapId != playerCoords.MapId)
                continue;

            var diff = detected.Coordinates.Position - new Vector2(0.5f, 0.5f) - playerCoords.Position;
            diff.X = Cap(diff.X, vpWidth);
            diff.Y = Cap(diff.Y, vpHeight);
            args.WorldHandle.DrawTexture(detected.QueenEye ? queenEye : blip, playerCoords.Position + diff);
        }
    }

    private static float Cap(float value, float size)
    {
        var max = size / 2f - 0.5f;
        return Math.Clamp(value, -max, max);
    }
}
