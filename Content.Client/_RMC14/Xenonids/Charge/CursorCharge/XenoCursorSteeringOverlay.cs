// Content.Client/_RMC14/Xenonids/Charge/CursorCharge/XenoCursorSteeringOverlay.cs

using System.Numerics;
using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client._RMC14.Xenonids.Charge.CursorCharge;

public sealed class XenoCursorSteeringOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    // If your texture's default artwork doesn't point "east" (+X), offset it here.
    // e.g. Angle.FromDegrees(90) if the sprite is drawn pointing "north" by default.
    private static readonly Angle TextureAngleOffset = Angle.FromDegrees(270);

    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<XenoChargerStateComponent> _steeringQuery;
    private readonly Texture _headingTexture;

    public XenoCursorSteeringOverlay(IEntityManager ents)
    {
        _player = IoCManager.Resolve<IPlayerManager>();
        _transform = ents.System<SharedTransformSystem>();
        _steeringQuery = ents.GetEntityQuery<XenoChargerStateComponent>();

        var resCache = IoCManager.Resolve<IResourceCache>();
        _headingTexture = resCache
            .GetResource<TextureResource>("/Textures/_RMC14/Effects/charge_indicator/charge_indicator.png")
            .Texture;
    }

    // Fixed world-unit size of the texture (width x height of the drawn box).
    private const float TextureSize = 1.4f;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player)
            return;

        if (!_steeringQuery.TryComp(player, out var steering))
            return;

        if (steering.MoveState != XenoChargerMoveState.Charging)
            return;

        var origin = _transform.GetMapCoordinates(player);
        if (origin.MapId != args.MapId)
            return;

        var headingVec = (Vector2)steering.CurrentHeading.ToVec();
        if (headingVec.LengthSquared() <= 0f)
            return;

        DrawHeadingTexture(args.WorldHandle, origin.Position, headingVec);
    }

// How far to offset the texture from the player's center, along the heading direction.
    private const float HeadingOffset = 0.7f;

    private void DrawHeadingTexture(DrawingHandleWorld handle, Vector2 center, Vector2 heading)
    {
        var angle = new Angle(Math.Atan2(heading.Y, heading.X)) + TextureAngleOffset;

        var normalizedHeading = Vector2.Normalize(heading);
        var offsetCenter = center + normalizedHeading * HeadingOffset;

        var rect = new Box2(-TextureSize / 2f, -TextureSize / 2f, TextureSize / 2f, TextureSize / 2f);
        var rotated = new Box2Rotated(rect.Translated(offsetCenter), angle, offsetCenter);

        handle.DrawTextureRect(_headingTexture, rotated);
    }
}
