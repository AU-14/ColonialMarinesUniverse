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
    private const float ReelSize = 0.425f;
    private const float ReelGap = 1f / EyeManager.PixelsPerMeter;
    private const float Padding = 1f / EyeManager.PixelsPerMeter;
    private const float HeadMargin = 0.1f;
    private const float LineWidth = 1f / EyeManager.PixelsPerMeter;
    private const int VisibleRows = 1;

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

            if (gambler.SpinStart is not { } start || !_reels.TryGetSpin(uid, gambler, out var strips))
                continue;

            var reels = strips.Count;
            var width = reels * ReelSize + (reels - 1) * ReelGap;
            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var bottom = sprite.Offset.Y + bounds.Height / 2 + HeadMargin;
            var windowHeight = ReelSize * (VisibleRows * 2 + 1);
            var lineY = bottom + windowHeight / 2;

            var worldPos = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Intersects(Box2.CenteredAround(worldPos, new Vector2(width + 2, 8))))
                continue;

            handle.SetTransform(Matrix3x2.Multiply(rotation, Matrix3Helpers.CreateTranslation(worldPos)));

            var left = sprite.Offset.X - width / 2;
            var right = left + width;
            handle.DrawRect(new Box2(left - Padding, bottom - Padding, right + Padding, bottom + windowHeight + Padding),
                Color.Black.WithAlpha(0.6f));

            for (var i = 0; i < reels; i++)
            {
                var x = left + i * (ReelSize + ReelGap);
                var position = CMUXenoGambleSpin.GetReelPosition(gambler, start, time, i);
                var nearest = (int) Math.Round(position);
                for (var k = nearest - VisibleRows - 1; k <= nearest + VisibleRows + 1; k++)
                {
                    // Icons further along the reel sit above the line & roll down onto it
                    var offset = (float) (k - position);
                    if (Math.Abs(offset) > VisibleRows + 0.5f || k == nearest && _reels.GetPop(gambler, i, time) > 0)
                        continue;

                    var alpha = Math.Clamp(1 - Math.Abs(offset) / (VisibleRows + 0.6f), 0, 1);
                    var y = lineY + offset * ReelSize;
                    DrawSlot(handle, strips[i].Get(k), new Box2(x, y - ReelSize / 2, x + ReelSize, y + ReelSize / 2), alpha, time);
                }
            }

            // winning line
            var lineColor = Color.White.WithAlpha(0.35f);
            handle.DrawRect(new Box2(left, lineY + ReelSize / 2, right, lineY + ReelSize / 2 + LineWidth), lineColor);
            handle.DrawRect(new Box2(left, lineY - ReelSize / 2 - LineWidth, right, lineY - ReelSize / 2), lineColor);

            // growing icon sits on top of its neighbours
            for (var i = 0; i < reels; i++)
            {
                var pop = _reels.GetPop(gambler, i, time);
                if (pop <= 0)
                    continue;

                var x = left + i * (ReelSize + ReelGap);
                var position = CMUXenoGambleSpin.GetReelPosition(gambler, start, time, i);
                var box = new Box2(x, lineY - ReelSize / 2, x + ReelSize, lineY + ReelSize / 2)
                    .Scale(1 + gambler.ReelPopScale * pop);

                DrawSlot(handle, strips[i].Get((int) Math.Round(position)), box, 1, time);
                handle.DrawRect(box, _reels.GetPopColor(gambler, i, time).WithAlpha(pop * 0.85f));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawSlot(DrawingHandleWorld handle, CMUXenoGambleReelEntry entry, Box2 box, float alpha, TimeSpan time)
    {
        var modulate = Color.White.WithAlpha(alpha);
        handle.DrawTextureRect(entry.Icon, box, modulate);
        handle.DrawTextureRect(_reels.GetFrame(entry.Frame, time), box, modulate);
    }
}
