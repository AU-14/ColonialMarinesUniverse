using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Dropship.TacticalLand;

/// <summary>Shows the upper deck while its real grid is still in FTL and outside the observer's PVS.</summary>
public sealed class MohawkApproachRoofOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();
    private readonly IResourceCache _resources = IoCManager.Resolve<IResourceCache>();
    private readonly Dictionary<EntityUid, IRenderTexture> _roofs = new();
    private readonly ShaderInstance _unshaded = IoCManager.Resolve<IPrototypeManager>()
        .Index(UnshadedShader).Instance();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public MohawkApproachRoofOverlay(IEntityManager entities, IGameTiming timing)
    {
        _entities = entities;
        _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        // An airborne roof passes above people and structures, while the beacons stay on the ground.
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.Overdoors + 1;
    }

    public void Invalidate(EntityUid uid)
    {
        if (_roofs.Remove(uid, out var texture))
            texture.Dispose();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.EntityQueryEnumerator<MohawkLandingBorderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var approach, out var transform))
        {
            if (transform.MapID != args.MapId || approach.RoofTiles.Count == 0 || approach.RoofBounds.IsEmpty())
                continue;

            var progress = MohawkApproachAnimation.Progress(approach, _timing.CurTime);
            if (progress >= 1f)
                continue;

            var matrix = _transform.GetWorldMatrix(uid);
            if (!args.WorldAABB.Enlarged(approach.RoofBounds.Size.Length() + 6f).Contains(matrix.Translation))
                continue;

            if (!_roofs.TryGetValue(uid, out var roof))
            {
                roof = CreateRoof(args.RenderHandle.DrawingHandleScreen, approach);
                _roofs.Add(uid, roof);
            }

            var height = MohawkApproachAnimation.Height(progress);
            var scale = 1f + height * 0.025f;
            var center = approach.RoofBounds.Center;
            // Height is up on the observer's screen, independent of the ship heading or camera rotation.
            var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
            var rise = (-eyeRotation).RotateVec(new Vector2(0, height));
            handle.SetTransform(Matrix3x2.CreateTranslation(-center) * Matrix3x2.CreateScale(scale) *
                Matrix3x2.CreateTranslation(center) * matrix * Matrix3x2.CreateTranslation(rise));

            // Reveal the aircraft as it comes into view, then hand off to the real decks at touchdown.
            var alpha = MohawkApproachAnimation.RoofOpacity(progress);
            handle.UseShader(_unshaded);
            handle.DrawTextureRect(roof.Texture, approach.RoofBounds, Color.White.WithAlpha(alpha));
            handle.UseShader(null);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private IRenderTexture CreateRoof(DrawingHandleScreen screen, MohawkLandingBorderComponent approach)
    {
        const float pixelsPerMeter = 32f;
        var bounds = approach.RoofBounds;
        var size = bounds.Size * pixelsPerMeter;
        var target = _clyde.CreateRenderTarget(new Vector2i((int) MathF.Ceiling(size.X), (int) MathF.Ceiling(size.Y)),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "mohawk-approach-roof");
        var previous = screen.GetTransform();
        screen.RenderInRenderTarget(target, () =>
        {
            screen.SetTransform(Matrix3x2.Identity);
            foreach (var tile in approach.RoofTiles)
            {
                var texture = _resources.GetResource<TextureResource>(new ResPath(tile.Texture)).Texture;
                var position = new Vector2(tile.Position.X - bounds.Left, bounds.Top - tile.Position.Y) * pixelsPerMeter;
                var source = UIBox2.FromDimensions(new Vector2(tile.Variant * pixelsPerMeter, 0), new Vector2(pixelsPerMeter));
                screen.DrawTextureRectRegion(texture, UIBox2.FromDimensions(position - new Vector2(16), new Vector2(32)), source);
            }
        }, Color.Transparent);
        screen.SetTransform(previous);
        return target;
    }

    protected override void DisposeBehavior()
    {
        foreach (var target in _roofs.Values)
            target.Dispose();
        _roofs.Clear();
        base.DisposeBehavior();
    }
}
