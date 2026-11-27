using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.TargetLock;

/// <summary>
///     A picture of someone taken the first time it is drawn, which is the moment of the lock: the entity is drawn once
///     into a texture and from then on only the texture is shown, so it does not change if they move, get hurt or
///     change clothes afterwards.
/// </summary>
public sealed class CMUTargetLockSnapshotView : Control
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    private readonly EntityUid? _entity;
    private readonly Vector2i _size;
    private IRenderTexture? _snapshot;
    private bool _taken;

    public CMUTargetLockSnapshotView(EntityUid? entity, Vector2i size)
    {
        IoCManager.InjectDependencies(this);
        _entity = entity;
        _size = size;
        MinSize = new Vector2(size.X, size.Y);
        HorizontalAlignment = HAlignment.Center;
    }

    protected override void Draw(IRenderHandle handle)
    {
        if (!_taken)
        {
            _taken = true;
            Take(handle);
        }

        if (_snapshot == null)
            return;

        handle.DrawingHandleScreen.DrawTextureRect(_snapshot.Texture, UIBox2.FromDimensions(Vector2.Zero, PixelSize));
    }

    private void Take(IRenderHandle handle)
    {
        if (_entity is not { } uid ||
            !_entities.TryGetComponent(uid, out SpriteComponent? sprite) ||
            !_entities.TryGetComponent(uid, out TransformComponent? xform))
        {
            return;
        }

        var target = _clyde.CreateRenderTarget(_size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "cmu-target-lock-snapshot");
        var transform = _entities.System<SharedTransformSystem>();
        handle.RenderInRenderTarget(target, () =>
        {
            handle.DrawEntity(uid, new Vector2(_size.X, _size.Y) / 2, new Vector2(2, 2), Angle.Zero, Angle.Zero,
                Direction.South, sprite, xform, transform);
        }, Color.Transparent);

        _snapshot = target;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _snapshot?.Dispose();
    }
}
