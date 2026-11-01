using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Unfiltered source pixels and identical CPU/GPU cutout sampling. No world queries.</summary>
public sealed class CMU3DSceneSurfaces
{
    public const int MaximumImageSize = 256;
    public const int Columns = 64;
    public const int Maximum = CMU3DSurfacePrototype.MaximumAtlasIndex;
    public readonly Rgba32[] Information = new Rgba32[Maximum + 1];
    private readonly Dictionary<ushort, (int Width, int Height, Rgba32[] Pixels)> _images = [];
    public int CellSize { get; private set; } = 1;

    public int Width => _images.Count == 0 ? 1 : CellSize * Columns;
    public int Height => _images.Count == 0 ? 1 : ((_images.Keys.Max() + Columns - 1) / Columns) * CellSize;

    public void Add(ushort index, int width, int height, Rgba32[] pixels)
    {
        if (index is 0 or > Maximum || _images.ContainsKey(index) || width is < 1 or > MaximumImageSize || height is < 1 or > MaximumImageSize || pixels.Length != width * height)
            throw new ArgumentException("Surface images need unique nonzero slots and bounded pixel dimensions.");
        while (CellSize < Math.Max(width, height))
            CellSize *= 2;
        _images.Add(index, (width, height, pixels));
        Information[index - 1] = new Rgba32((byte) (width - 1), (byte) (height - 1), 0, 255);
    }

    public static CMU3DSceneSurfaces Load(IPrototypeManager prototypes, IResourceCache resources)
    {
        var result = new CMU3DSceneSurfaces();
        foreach (var surface in prototypes.EnumeratePrototypes<CMU3DSurfacePrototype>())
        {
            using var stream = resources.ContentFileRead(surface.Texture);
            using var image = Image.Load<Rgba32>(stream);
            var pixels = new Rgba32[image.Width * image.Height];
            image.GetPixelSpan().CopyTo(pixels);
            result.Add(surface.AtlasIndex, image.Width, image.Height, pixels);
        }
        return result;
    }

    public Rgba32[] AtlasPixels()
    {
        var result = new Rgba32[Width * Height];
        foreach (var (index, image) in _images)
        {
            var x = (index - 1) % Columns * CellSize;
            var y = (index - 1) / Columns * CellSize;
            for (var row = 0; row < image.Height; row++)
                image.Pixels.AsSpan(row * image.Width, image.Width).CopyTo(result.AsSpan((y + row) * Width + x, image.Width));
        }
        return result;
    }

    public static Vector2 Coordinates(Vector3 local, CMU3DSurfaceAxis axis, float verticalScale = 1)
    {
        return axis switch
        {
            CMU3DSurfaceAxis.XY => new Vector2(local.X + .5f, .5f - local.Y),
            CMU3DSurfaceAxis.YZ => new Vector2(.5f - local.Y, 1 - (local.Z + .5f) * verticalScale),
            _ => new Vector2(local.X + .5f, 1 - (local.Z + .5f) * verticalScale),
        };
    }

    public Rgba32 Sample(ushort index, Vector2 uv)
    {
        if (!_images.TryGetValue(index, out var image))
            return new Rgba32(0, 0, 0, 0);
        var x = Math.Clamp((int) MathF.Floor(uv.X * image.Width), 0, image.Width - 1);
        var y = Math.Clamp((int) MathF.Floor(uv.Y * image.Height), 0, image.Height - 1);
        return image.Pixels[y * image.Width + x];
    }

    public bool OpaqueAt(CMU3DSceneBox box, Vector3 normalizedLocal)
    {
        var uv = Coordinates(normalizedLocal, box.SurfaceAxis, box.SurfaceVScale);
        if (box.SurfaceFlipU)
            uv.X = 1 - uv.X;
        return Sample(box.SurfaceIndex, uv).A >= 128;
    }
}
