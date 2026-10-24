using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Threats.Mobs.Xeno.Gambler;

public sealed class CMUXenoGamblerReelOverlay : Overlay
{
    private const float ReelSize = 0.5f;
    private const float ReelGap = 1f / EyeManager.PixelsPerMeter;
    private const float Padding = 1f / EyeManager.PixelsPerMeter;
    private const float HeadMargin = 0.2f;

    private readonly IEntityManager _entities;
    private readonly CMUXenoGamblerReelSystem _reels;
    private readonly SharedContainerSystem _container;
    private readonly IPlayerManager _player;
    private readonly SpriteSystem _sprite;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CMUXenoGamblerReelOverlay(IEntityManager entities, CMUXenoGamblerReelSystem reels)
    {
        _entities = entities;
        _reels = reels;
        _container = entities.System<SharedContainerSystem>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _sprite = entities.System<SpriteSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var local = _player.LocalEntity;
        var time = _timing.CurTime;

        var gamblers = _entities.EntityQueryEnumerator<CMUXenoGamblerComponent, SpriteComponent, TransformComponent>();
        while (gamblers.MoveNext(out var uid, out var gambler, out var sprite, out var xform))
        {
            if (gambler.SpinStart == null ||
                xform.MapID != args.MapId ||
                !sprite.Visible ||
                (uid != local && sprite.Color.A < 0.5f) ||
                _container.IsEntityInContainer(uid))
            {
                continue;
            }

            if (!_reels.TryGetSpin(uid, gambler, out var strip, out var step))
                continue;

            var reels = gambler.WinnerIcons.Count;
            var width = reels * ReelSize + (reels - 1) * ReelGap;
            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var bottom = sprite.Offset.Y + bounds.Height / 2 + HeadMargin;

            var worldPos = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Intersects(Box2.CenteredAround(worldPos, new Vector2(width + 2, 6))))
                continue;

            handle.SetTransform(Matrix3x2.Multiply(rotation, Matrix3Helpers.CreateTranslation(worldPos)));

            var left = sprite.Offset.X - width / 2;
            var background = new Box2(left - Padding, bottom - Padding, left + width + Padding, bottom + ReelSize + Padding);
            handle.DrawRect(background, Color.Black.WithAlpha(0.6f));

            for (var i = 0; i < reels; i++)
            {
                var index = CMUXenoGambleSpin.GetStripIndex(gambler, i, step);
                if (index >= strip.Count)
                    break;

                var x = left + i * (ReelSize + ReelGap);
                var box = new Box2(x, bottom, x + ReelSize, bottom + ReelSize);
                var entry = strip[index];
                handle.DrawTextureRect(entry.Icon, box);
                handle.DrawTextureRect(_reels.GetFrame(entry.Frame, time), box);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
