using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Industry;

/// <summary>Bounded, allocation-free particle geometry. Motion is sampled by each emitter.</summary>
internal sealed class CavernParticleBatch
{
    private static readonly ProtoId<ShaderPrototype> ParticleShader = "CMUCascadeParticle";
    private static readonly ProtoId<ShaderPrototype> FilamentShader = "unshaded";
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>().Index(ParticleShader).InstanceUnique();
    private readonly ShaderInstance _unshaded = IoCManager.Resolve<IPrototypeManager>().Index(FilamentShader).Instance();
    private readonly DrawVertexUV2DColor[] _motes = new DrawVertexUV2DColor[12288];
    private readonly DrawVertexUV2DColor[] _shards = new DrawVertexUV2DColor[2048];
    private readonly DrawVertexUV2DColor[] _filaments = new DrawVertexUV2DColor[8192];
    private int _moteCount;
    private int _shardCount;
    private int _filamentCount;

    public void Clear() { _moteCount = 0; _shardCount = 0; _filamentCount = 0; }

    public void Mote(Vector2 p, Vector2 size, float angle, Color color, float material = 0, float seed = 0)
    {
        if (color.A <= .002f || _moteCount + 6 > _motes.Length)
            return;
        var x = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size.X;
        var y = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * size.Y;
        var tint = Color.FromSrgb(color);
        var data = new Vector2(material, seed);
        Vertex(p - x - y, new(0, 0));
        Vertex(p + x - y, new(1, 0));
        Vertex(p - x + y, new(0, 1));
        Vertex(p - x + y, new(0, 1));
        Vertex(p + x - y, new(1, 0));
        Vertex(p + x + y, new(1, 1));
        void Vertex(Vector2 at, Vector2 uv) => _motes[_moteCount++] = new(at, uv, tint) { UV2 = data };
    }

    public void Trail(Vector2 a, Vector2 b, float width, Color color)
    {
        var delta = b - a;
        var length = delta.Length();
        if (length < .001f)
            return;
        Mote((a + b) * .5f, new(length * .5f + width, width), MathF.Atan2(delta.Y, delta.X), color);
    }

    public void Shard(Vector2 p, float size, float angle, Color color)
    {
        if (_shardCount + 6 > _shards.Length)
            return;
        var x = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
        var y = new Vector2(-x.Y, x.X) * .55f;
        var tint = Color.FromSrgb(color);
        var shade = new Color(tint.R * .55f, tint.G * .55f, tint.B * .55f, tint.A);
        _shards[_shardCount++] = new(p - x, tint);
        _shards[_shardCount++] = new(p + y, tint);
        _shards[_shardCount++] = new(p + x, tint);
        _shards[_shardCount++] = new(p - x, shade);
        _shards[_shardCount++] = new(p + x, shade);
        _shards[_shardCount++] = new(p - y, shade);
    }

    public void Filament(Vector2 a, Vector2 b, float halfWidth, Color color)
    {
        var delta = b - a;
        if (delta.LengthSquared() < .000001f || color.A <= .002f || _filamentCount + 6 > _filaments.Length)
            return;
        var side = Vector2.Normalize(new Vector2(-delta.Y, delta.X)) * halfWidth;
        var tint = Color.FromSrgb(color);
        _filaments[_filamentCount++] = new(a - side, tint);
        _filaments[_filamentCount++] = new(a + side, tint);
        _filaments[_filamentCount++] = new(b - side, tint);
        _filaments[_filamentCount++] = new(b - side, tint);
        _filaments[_filamentCount++] = new(a + side, tint);
        _filaments[_filamentCount++] = new(b + side, tint);
    }

    public void Draw(DrawingHandleWorld h, float now)
    {
        h.UseShader(null);
        if (_shardCount > 0)
            h.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _shards.AsSpan(0, _shardCount));
        if (_moteCount > 0)
        {
            _shader.SetParameter("clock", now);
            h.UseShader(_shader);
            h.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _motes.AsSpan(0, _moteCount));
        }
        if (_filamentCount > 0)
        {
            // Narrow electrical cores remain readable over the soft particle halo and mist.
            h.UseShader(_unshaded);
            h.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _filaments.AsSpan(0, _filamentCount));
        }
        h.UseShader(null);
    }

    public void Dispose() => _shader.Dispose();
}
