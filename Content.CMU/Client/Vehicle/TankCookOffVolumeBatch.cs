using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Vehicle;

/// <summary>Depth-sorted density volumes and analytic surfaces. No explosion textures or particle entities.</summary>
internal sealed class TankCookOffVolumeBatch : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> VolumeShader = "CMUTankCookOffVolume";
    private readonly record struct DrawItem(TankCookOffParticleScene.Sample Sample, Vector2 Origin);
    private readonly List<DrawItem> _draws = new(4096);
    // Flush and continue if several tanks rupture together.
    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[4096 * 6];
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>().Index(VolumeShader).InstanceUnique();

    public void Clear() => _draws.Clear();
    public void Add(TankCookOffParticleScene scene, float age, Vector2 origin)
    {
        for (var i = 0; i < scene.ParticleCount; i++)
            if (scene.TrySample(i, age, out var sample)) _draws.Add(new(sample, origin));
    }
    public void Draw(DrawingHandleWorld handle)
    {
        _draws.Sort(static (a, b) =>
        {
            var layer = a.Sample.Layer.CompareTo(b.Sample.Layer);
            return layer != 0 ? layer : (b.Origin.Y + b.Sample.Depth).CompareTo(a.Origin.Y + a.Sample.Depth);
        });
        handle.UseShader(_shader);
        var count = 0;
        foreach (var draw in _draws)
        {
            if (count + 6 > _vertices.Length)
            {
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, count));
                count = 0;
            }
            var s = draw.Sample;
            var position = draw.Origin + s.Position;
            var x = new Vector2(MathF.Cos(s.Rotation), MathF.Sin(s.Rotation)) * s.Size.X * .5f;
            var y = new Vector2(-MathF.Sin(s.Rotation), MathF.Cos(s.Rotation)) * s.Size.Y * .5f;
            var color = new Color(1, 1, 1, s.Color.W);
            var data = new Vector2(s.Frame, (int)s.Material + s.Variant / 1024f);
            Vertex(position - x - y, new(0, 0)); Vertex(position + x - y, new(1, 0));
            Vertex(position - x + y, new(0, 1)); Vertex(position - x + y, new(0, 1));
            Vertex(position + x - y, new(1, 0)); Vertex(position + x + y, new(1, 1));
            void Vertex(Vector2 p, Vector2 uv) => _vertices[count++] = new(p, uv, color) { UV2 = data };
        }
        if (count > 0) handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, count));
        handle.UseShader(null);
    }
    public void Dispose() => _shader.Dispose();
}
