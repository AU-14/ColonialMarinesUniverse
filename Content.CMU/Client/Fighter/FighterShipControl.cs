using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fighter;

/// <summary>One view of the actual airframe and buckled crew, sharing their world scale and positions.</summary>
public sealed class FighterShipControl : Control
{
    private readonly IEntityManager _entities = IoCManager.Resolve<IEntityManager>();
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private FighterAircraftComponent? _aircraft;
    private EntityUid? _pilot;
    private EntityUid? _observer;
    private Vector2 _origin;
    private Vector2 _boundsCenter;
    private Vector2 _scale;
    private Angle _frameRotation;
    public Vector2 EffectOffset;

    public Vector2 Point(float x, float y) => GlobalPixelPosition + PixelSize * new Vector2(x, y) + EffectOffset;

    public Vector2 MountPosition(EntityUid mount)
    {
        if (!_entities.EntityExists(mount) || _scale == Vector2.Zero) return Point(.5f, .3f);
        var offset = (-_frameRotation).RotateVec(_transform.GetWorldPosition(mount) - _origin) - _boundsCenter;
        return GlobalPixelPosition + PixelSize / 2f + new Vector2(offset.X, -offset.Y) * EyeManager.PixelsPerMeter * _scale + EffectOffset;
    }

    public FighterShipControl()
    {
        _transform = _entities.System<SharedTransformSystem>();
        _sprites = _entities.System<SpriteSystem>();
        RectClipContent = true;
    }

    public void SetAircraft(FighterAircraftComponent aircraft, EntityUid? pilot, EntityUid? observer)
    {
        _aircraft = aircraft;
        _pilot = pilot;
        _observer = observer;
    }

    protected override void Draw(IRenderHandle handle)
    {
        if (_aircraft?.Hull is not { } hull ||
            !_entities.TryGetComponent(hull, out SpriteComponent? hullSprite) ||
            !_entities.TryGetComponent(hull, out TransformComponent? hullTransform))
            return;

        var frameRotation = _transform.GetWorldRotation(hullTransform.ParentUid);
        var hullRotation = _transform.GetWorldRotation(hull) - frameRotation;
        _sprites.ForceUpdate(hull);
        var bounds = _sprites.CalculateBounds((hull, hullSprite), Vector2.Zero, hullRotation, Angle.Zero).CalcBoundingBox();
        var extent = bounds.Size * EyeManager.PixelsPerMeter;
        if (extent.X <= 0 || extent.Y <= 0)
            return;
        var scale = new Vector2(Math.Min(PixelSize.X / extent.X, PixelSize.Y / extent.Y));
        var origin = _transform.GetWorldPosition(hull);
        _origin = origin;
        _boundsCenter = bounds.Center;
        _scale = scale;
        _frameRotation = frameRotation;

        DrawPart(hull);
        if (_entities.TryGetComponent(hullTransform.ParentUid, out FighterWeaponsComponent? weapons))
            foreach (var point in weapons.Hardpoints)
                if (_entities.TryGetComponent(point, out FighterHardpointComponent? mount) && !mount.Internal)
                    DrawPart(point);
        DrawPart(_aircraft.FrontSeat);
        DrawPart(_aircraft.RearSeat);
        DrawPart(_pilot);
        DrawPart(_observer);
        DrawPart(_aircraft.Canopy);
        return;

        void DrawPart(EntityUid? part)
        {
            if (part is not { } uid ||
                !_entities.TryGetComponent(uid, out SpriteComponent? sprite) ||
                !_entities.TryGetComponent(uid, out TransformComponent? transform))
                return;

            _sprites.ForceUpdate(uid);
            var offset = (-frameRotation).RotateVec(_transform.GetWorldPosition(uid) - origin) - bounds.Center;
            var position = PixelSize / 2f + new Vector2(offset.X, -offset.Y) * EyeManager.PixelsPerMeter * scale + EffectOffset;
            var rotation = _transform.GetWorldRotation(uid) - frameRotation;
            handle.DrawEntity(uid, position, scale, rotation, sprite: sprite, xform: transform, xformSystem: _transform);
        }
    }
}
