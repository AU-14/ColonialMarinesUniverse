using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Overlays;

/// <summary>A single owned, premultiplied snapshot of an infrequently changing content drawing.</summary>
internal sealed class CMUCachedDrawing(IClyde clyde, IPrototypeManager prototypes) : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "CMUCachedDrawing";
    private IRenderTexture? _target;
    public Texture? Texture => _target?.Texture;
    public ShaderInstance Shader { get; } = prototypes.Index(ShaderId).Instance();

    public void Update(IRenderHandle render, Vector2i size, Color modulate, Action<DrawingHandleScreen> draw)
    {
        if (_target == null || _target.Size != size)
        {
            _target?.Dispose();
            _target = clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb, name: "cmu-cached-drawing");
        }

        var handle = render.DrawingHandleScreen;
        var oldModulate = handle.Modulate;
        try
        {
            handle.RenderInRenderTarget(_target, () =>
            {
                render.SetScissor(null);
                handle.SetTransform(Matrix3x2.Identity);
                handle.UseShader(null);
                handle.Modulate = modulate;
                draw(handle);
            }, Color.Transparent);
        }
        finally
        {
            // RenderInRenderTarget restores render state, but UI modulation belongs to the handle.
            handle.Modulate = oldModulate;
        }
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
    }
}
