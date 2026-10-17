using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Fighter;

/// <summary>Fixed-size geometry for cockpit and world effects; no particle entities or per-frame allocations.</summary>
internal sealed class FighterParticleBatch : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ParticleShader = "CMUFighterParticles";
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index(ParticleShader).InstanceUnique();
    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[12288];
    private int _count;

    public void Clear() => _count = 0;

    public void Mote(Vector2 position, Vector2 size, Color color, float angle = 0, bool smoke = false, float seed = 0)
    {
        if (_count + 6 > _vertices.Length || color.A < .002f) return;
        var x = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size.X;
        var y = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * size.Y;
        var tint = Color.FromSrgb(color);
        var data = new Vector2(smoke ? 1 : 0, seed);
        Add(position - x - y, new(0, 0));
        Add(position + x - y, new(1, 0));
        Add(position - x + y, new(0, 1));
        Add(position - x + y, new(0, 1));
        Add(position + x - y, new(1, 0));
        Add(position + x + y, new(1, 1));
        return;

        void Add(Vector2 p, Vector2 uv) => _vertices[_count++] = new(p, uv, tint) { UV2 = data };
    }

    public void Trail(Vector2 start, Vector2 end, float width, Color color)
    {
        var delta = end - start;
        Mote((start + end) * .5f, new Vector2(delta.Length() * .5f + width, width), color,
            MathF.Atan2(delta.Y, delta.X));
    }

    public void Draw(DrawingHandleBase handle, float clock)
    {
        if (_count == 0) return;
        _shader.SetParameter("clock", clock);
        handle.UseShader(_shader);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, _count));
        handle.UseShader(null);
    }

    public void Dispose() => _shader.Dispose();
}
