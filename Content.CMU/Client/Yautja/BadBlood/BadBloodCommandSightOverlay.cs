using System.Numerics;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 FLOW-701 (D3): the Hunt Leader's command sight. Draws the clan's own (living Bad Bloods, clan beasts and Hunt
/// Grounds) within <see cref="BadBloodCommandSightComponent.Range"/> over walls, faintly, for a local player who has
/// the component. Nothing else is shown through walls: marines and prey still need eyes or a Tracker.
/// </summary>
public sealed class BadBloodCommandSightOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _players = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly ContainerSystem _container;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private const float Transparency = 0.55f;

    public BadBloodCommandSightOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _container = _entity.System<ContainerSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } player ||
            !_entity.TryGetComponent(player, out BadBloodCommandSightComponent? sight) ||
            !_entity.TryGetComponent(player, out TransformComponent? playerXform))
        {
            return;
        }

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var center = _transform.GetWorldPosition(playerXform);
        var range = sight.Range * sight.Range;
        var map = playerXform.MapID;

        var clan = _entity.EntityQueryEnumerator<YautjaBadBloodComponent, SpriteComponent, TransformComponent>();
        while (clan.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            Render(uid, player, sprite, xform, map, center, range, handle, eyeRot);
        }

        var beasts = _entity.EntityQueryEnumerator<BadBloodBeastComponent, SpriteComponent, TransformComponent>();
        while (beasts.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            Render(uid, player, sprite, xform, map, center, range, handle, eyeRot);
        }

        var grounds = _entity.EntityQueryEnumerator<BadBloodHuntGroundComponent, SpriteComponent, TransformComponent>();
        while (grounds.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            Render(uid, player, sprite, xform, map, center, range, handle, eyeRot);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void Render(EntityUid uid,
        EntityUid player,
        SpriteComponent sprite,
        TransformComponent xform,
        Robust.Shared.Map.MapId map,
        Vector2 center,
        float range,
        DrawingHandleWorld handle,
        Angle eyeRot)
    {
        if (uid == player || xform.MapID != map || !sprite.Visible)
            return;

        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        if ((position - center).LengthSquared() > range)
            return;

        if (_container.IsEntityOrParentInContainer(uid, xform: xform))
            return;

        // a cloaked clanmate stays cloaked to everyone, the Hunt Leader too
        if (_entity.HasComponent<EntityActiveInvisibleComponent>(uid))
            return;

        var color = sprite.Color;
        _sprite.SetColor((uid, sprite), color * Color.White.WithAlpha(Transparency));
        _sprite.RenderSprite((uid, sprite), handle, eyeRot, rotation, position);
        _sprite.SetColor((uid, sprite), color);
    }
}

/// <summary>
/// CMU14 FLOW-701: keeps <see cref="BadBloodCommandSightOverlay"/> on the overlay manager.
/// </summary>
public sealed class BadBloodCommandSightSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        _overlays.AddOverlay(new BadBloodCommandSightOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BadBloodCommandSightOverlay>();
    }
}
