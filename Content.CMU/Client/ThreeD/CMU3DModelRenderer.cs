using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.ThreeD;

/// <summary>
/// Content-only solid-part preview renderer. A cached BSP splits axis-aligned faces and produces
/// correct back-to-front order without a hardware depth buffer. Source models are never mutated.
/// </summary>
public sealed class CMU3DModelRenderer
{
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.4f, -0.6f, 1));
    private readonly List<CMU3DModelFace> _faces = [];
    private readonly List<CMU3DModelFace> _ordered = [];
    private readonly CMU3DModelBsp _bsp = new();
    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[8190];

    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }
    public int PartCount { get; private set; }
    public bool WithinBudget { get; private set; } = true;

    public void SetModel(CMU3DModelPrototype? model)
    {
        _faces.Clear();
        _ordered.Clear();
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(float.MinValue);
        PartCount = 0;
        var hasRounded = false;
        if (model != null)
        {
            foreach (var part in model.Parts)
            {
                if (!part.Valid)
                    continue;
                PartCount++;
                part.Bounds(out var min, out var max);
                Min = Vector3.Min(Min, min);
                Max = Vector3.Max(Max, max);
                hasRounded |= model.EquipmentOnly || part.Shape != CMU3DPartShape.Box || part.Yaw != 0 || part.Pitch != 0;
                if (!model.EquipmentOnly && part.Shape == CMU3DPartShape.Box && _faces.Count < CMU3DModelBsp.MaxInputFaces)
                    AddPart(part);
            }
        }
        if (PartCount == 0)
        {
            Min = new Vector3(-0.5f, -0.5f, 0);
            Max = new Vector3(0.5f, 0.5f, 1);
        }
        WithinBudget = !hasRounded && _bsp.TryBuild(_faces) && PartCount <= CMU3DModelBsp.MaxInputFaces / 6;
    }

    private void AddPart(CMU3DModelPart part)
    {
        var a = part.Min;
        var b = part.Max;
        for (var axis = 0; axis < 3; axis++)
        {
            AddFace(a, CMU3DModelFace.WithCoordinate(b, axis, CMU3DModelFace.Coordinate(a, axis)), axis, -1);
            AddFace(CMU3DModelFace.WithCoordinate(a, axis, CMU3DModelFace.Coordinate(b, axis)), b, axis, 1);
        }
        return;

        void AddFace(Vector3 low, Vector3 high, int axis, int facing)
        {
            var normal = CMU3DModelFace.WithCoordinate(Vector3.Zero, axis, facing);
            var brightness = 0.52f + Math.Max(0, Vector3.Dot(normal, Light)) * 0.48f;
            var color = new Color(part.Color.R * brightness, part.Color.G * brightness,
                part.Color.B * brightness, part.Color.A);
            _faces.Add(new CMU3DModelFace(low, high, axis, facing, color, _faces.Count));
        }
    }

    public void CollectVisibleFaces(Vector3 eye, List<CMU3DModelFace> output)
    {
        output.Clear();
        if (WithinBudget)
            _bsp.CollectVisibleFaces(eye, output);
    }

    public void Draw(DrawingHandleScreen handle, CMU3DCameraFrame camera, bool edges)
    {
        CollectVisibleFaces(camera.Origin, _ordered);
        var count = 0;
        foreach (var face in _ordered)
        {
            face.Corners(out var p0, out var p1, out var p2, out var p3);
            if (!camera.TryProject(p0, out var a) || !camera.TryProject(p1, out var b) ||
                !camera.TryProject(p2, out var c) || !camera.TryProject(p3, out var d))
                continue;
            if (count + 6 > _vertices.Length)
            {
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, count));
                count = 0;
            }
            // The per-vertex overload does not apply UI modulation or convert sRGB for us.
            var color = Color.FromSrgb(face.Color * handle.Modulate);
            _vertices[count++] = new(a, Vector2.Zero, color);
            _vertices[count++] = new(b, Vector2.Zero, color);
            _vertices[count++] = new(c, Vector2.Zero, color);
            _vertices[count++] = new(a, Vector2.Zero, color);
            _vertices[count++] = new(c, Vector2.Zero, color);
            _vertices[count++] = new(d, Vector2.Zero, color);
            if (!edges)
                continue;
            // Flush each face before its outline so later faces correctly cover farther outlines.
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, count));
            count = 0;
            var outline = new Color(0.04f, 0.07f, 0.09f, 0.5f);
            handle.DrawLine(a, b, outline);
            handle.DrawLine(b, c, outline);
            handle.DrawLine(c, d, outline);
            handle.DrawLine(d, a, outline);
        }
        if (count != 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, count));
    }
}
