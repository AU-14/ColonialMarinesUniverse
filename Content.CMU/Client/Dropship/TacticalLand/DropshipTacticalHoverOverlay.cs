using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Dropship.TacticalLand;

/// <summary>One cached alpha silhouette per ship, plus continuous ground-projected lift exhaust.</summary>
public sealed class DropshipTacticalHoverOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    private readonly IEntityManager _entities;
    private const float PixelsPerMeter = 32f;
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();
    private readonly IResourceCache _resources = IoCManager.Resolve<IResourceCache>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly IComponentFactory _factory = IoCManager.Resolve<IComponentFactory>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IEyeManager _eye = IoCManager.Resolve<IEyeManager>();
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private readonly PointLightSystem _lights;
    private static readonly ProtoId<ShaderPrototype> ExhaustShader = "CMUDropshipHoverExhaust";
    private readonly Dictionary<EntityUid, IRenderTexture> _silhouettes = new();
    private readonly Dictionary<EntityUid, Vector2> _nozzleThrust = new();
    private readonly ShaderInstance _exhaust = IoCManager.Resolve<IPrototypeManager>()
        .Index(ExhaustShader).InstanceUnique();

    public DropshipTacticalHoverOverlay(IEntityManager entities)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
        _lights = entities.System<PointLightSystem>();
        // Project onto floor coverings, while keeping walls and occupants above the ground effects.
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.HighFloorObjects + 1;
    }

    public void Invalidate(EntityUid uid)
    {
        if (_silhouettes.Remove(uid, out var target))
            target.Dispose();
    }

    public void RemoveNozzle(EntityUid uid) => _nozzleThrust.Remove(uid);

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var blend = 1f - MathF.Exp(-8f * args.DeltaSeconds);
        var washes = _entities.EntityQueryEnumerator<DropshipTacticalHoverDownwashComponent>();
        while (washes.MoveNext(out var uid, out var wash))
        {
            if (!wash.JetExhaust)
                continue;

            _nozzleThrust.TryGetValue(uid, out var current);
            var thrust = Vector2.Lerp(current, wash.ManeuverThrust, blend);
            _nozzleThrust[uid] = thrust;
            if (_entities.TryGetComponent<PointLightComponent>(uid, out var light))
            {
                // Light follows the same smoothed contact point as the shader. Its component
                // offset is hull-local, whereas the projected exhaust stays vertical in view.
                var rotation = _transform.GetWorldRotation(uid);
                var contact = GunshipHoverExhaust.GroundContactOffset(thrust, rotation, _eye.CurrentEye.Rotation);
                _lights.SetOffset(uid, (-rotation).RotateVec(contact), light);
            }
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var h = args.WorldHandle;
        var shadows = _entities.EntityQueryEnumerator<DropshipTacticalHoverShadowComponent, TransformComponent>();
        while (shadows.MoveNext(out var uid, out var shadow, out var xform))
        {
            if (xform.MapID != args.MapId || shadow.HullBounds.IsEmpty())
                continue;

            var matrix = _transform.GetWorldMatrix(uid);
            var bounds = shadow.HullBounds;
            if (!args.WorldAABB.Enlarged(bounds.Size.Length()).Contains(matrix.Translation))
                continue;

            if (!_silhouettes.TryGetValue(uid, out var target))
            {
                // This is a world-space pass. Get the separate screen handle for the
                // pixel-space render target instead of casting the active world handle.
                target = CreateSilhouette(args.RenderHandle.DrawingHandleScreen, shadow);
                _silhouettes.Add(uid, target);
            }

            h.SetTransform(matrix);
            // Blur the assembled alpha, not each tile: the hull stays solid without dark tile seams.
            h.DrawTextureRect(target.Texture, bounds, Color.Black.WithAlpha(.28f));
            for (var i = 0; i < 8; i++)
            {
                var angle = i * MathF.Tau / 8;
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * .075f;
                h.DrawTextureRect(target.Texture, bounds.Translated(offset), Color.Black.WithAlpha(.045f));
            }
        }

        _exhaust.SetParameter("clock", (float) _timing.CurTime.TotalSeconds);
        // Lift is vertical, not forward thrust. Keep the plume upright in the view while the
        // world-space nozzle position follows the ship, including when the pilot camera turns.
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var exhaustRotation = Matrix3Helpers.CreateRotation(-eyeRotation);
        var washes = _entities.EntityQueryEnumerator<DropshipTacticalHoverDownwashComponent, TransformComponent>();
        while (washes.MoveNext(out var uid, out var wash, out var xform))
        {
            if (!wash.JetExhaust || xform.MapID != args.MapId ||
                !args.WorldAABB.Enlarged(4f).Contains(_transform.GetWorldPosition(uid)))
                continue;

            // Each nozzle gets a stable independent phase. No networked particle entities are needed.
            _exhaust.SetParameter("seed", wash.Offset.X * 3.17f + wash.Offset.Y * 7.31f);
            _nozzleThrust.TryGetValue(uid, out var thrust);
            _exhaust.SetParameter("jetOffset", GunshipHoverExhaust.GroundDeflection(thrust,
                _transform.GetWorldRotation(uid), eyeRotation));
            h.SetTransform(exhaustRotation * Matrix3x2.CreateTranslation(_transform.GetWorldPosition(uid)));
            h.UseShader(_exhaust);
            h.DrawTextureRect(Texture.White, new Box2(-3f, -4f, 3f, 3f));
            h.UseShader(null);
        }

        h.SetTransform(Matrix3x2.Identity);
    }

    private IRenderTexture CreateSilhouette(DrawingHandleScreen screen, DropshipTacticalHoverShadowComponent shadow)
    {
        var bounds = shadow.HullBounds;
        var size = bounds.Size * PixelsPerMeter;
        var target = _clyde.CreateRenderTarget(
            new Vector2i((int) MathF.Ceiling(size.X), (int) MathF.Ceiling(size.Y)),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            name: "dropship-hover-silhouette");
        var previous = screen.GetTransform();
        screen.RenderInRenderTarget(target, () =>
        {
            screen.SetTransform(Matrix3x2.Identity);
            foreach (var tile in shadow.Tiles)
            {
                var texture = _resources.GetResource<TextureResource>(new ResPath(tile.Texture)).Texture;
                var position = ToPixel(tile.Position, bounds);
                var source = UIBox2.FromDimensions(new Vector2(tile.Variant * PixelsPerMeter, 0),
                    new Vector2(PixelsPerMeter));
                screen.DrawTextureRectRegion(texture,
                    UIBox2.FromDimensions(position - new Vector2(16), new Vector2(32)), source);
            }

            foreach (var part in shadow.Parts)
            {
                var prototype = _prototypes.Index<EntityPrototype>(part.Prototype);
                if (!prototype.TryComp<SpriteComponent>(out var sprite, _factory))
                    continue;

                var icon = _sprites.GetPrototypeIcon(prototype);
                var texture = icon.GetFrame(RsiDirection.South, 0);
                var position = ToPixel(part.Position + part.Rotation.RotateVec(sprite.Offset), bounds);
                var rotation = sprite.NoRotation ? 0f : -(float) (part.Rotation + sprite.Rotation).Theta;
                screen.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position));
                var half = (Vector2) texture.Size * sprite.Scale * .5f;
                screen.DrawTextureRect(texture, new UIBox2(-half, half));
            }
        }, Color.Transparent);
        screen.SetTransform(previous);
        return target;
    }

    private static Vector2 ToPixel(Vector2 position, Box2 bounds)
        => new((position.X - bounds.Left) * PixelsPerMeter, (bounds.Top - position.Y) * PixelsPerMeter);

    protected override void DisposeBehavior()
    {
        foreach (var target in _silhouettes.Values)
            target.Dispose();
        _silhouettes.Clear();
        _nozzleThrust.Clear();
        _exhaust.Dispose();
        base.DisposeBehavior();
    }
}
