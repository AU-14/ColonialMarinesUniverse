using System.Numerics;
using Content.Client.Resources;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared.Ghost.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Xenonids.JoinXeno;

public sealed class LarvaPoolOfferOverlay : Overlay
{
    private const string FontPath = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";
    private const int FontSize = 16;
    private const float VerticalOffset = 42;

    private static readonly Color PlentyOfTimeColor = Color.LimeGreen;
    private static readonly Color RunningLowColor = Color.Gold;
    private static readonly Color UrgentColor = Color.OrangeRed;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly Font _font;
    private readonly IPlayerManager _players;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public LarvaPoolOfferOverlay(
        IEntityManager entities,
        IGameTiming timing,
        IPlayerManager players,
        IResourceCache resources)
    {
        _entities = entities;
        _timing = timing;
        _font = resources.GetFont(FontPath, FontSize);
        _players = players;
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null ||
            _players.LocalEntity is not { } local ||
            !_entities.HasComponent<XenoComponent>(local) &&
            !_entities.HasComponent<GhostComponent>(local))
        {
            return;
        }

        var query = _entities.EntityQueryEnumerator<LarvaPoolOfferComponent, TransformComponent>();
        while (query.MoveNext(out _, out var offer, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPosition = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(worldPosition))
                continue;

            var seconds = Math.Max(0, (int) Math.Ceiling((offer.ExpiresAt - _timing.CurTime).TotalSeconds));
            var text = Loc.GetString("rmc-xeno-larva-pool-offer-counter", ("seconds", seconds));
            var color = seconds > 20
                ? PlentyOfTimeColor
                : seconds > 10
                    ? RunningLowColor
                    : UrgentColor;

            var screenPosition = args.ViewportControl.WorldToScreen(worldPosition);
            var size = args.ScreenHandle.DrawString(_font, screenPosition, text, Color.Transparent);
            var textPosition = screenPosition - new Vector2(size.X / 2, VerticalOffset);
            args.ScreenHandle.DrawString(_font, textPosition + Vector2.One, text, Color.Black);
            args.ScreenHandle.DrawString(_font, textPosition, text, color);
        }
    }
}
