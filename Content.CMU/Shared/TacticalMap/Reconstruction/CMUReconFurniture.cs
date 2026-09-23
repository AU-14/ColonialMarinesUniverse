using System.Numerics;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>
/// Small furniture assemblies shared by picking and the GPU. Coordinates are integral centimetres,
/// encoded losslessly into one 17-column RGBA8 lookup texture (header and two texels per part).
/// No per-entity meshes, geometry packets or frame-time model construction are needed.
/// </summary>
public static class CMUReconFurniture
{
    public const int FirstMaterial = (int) CMUReconMaterial.Chair;
    public const int MaxParts = 8;
    public const int TextureWidth = 1 + MaxParts * 2;
    public const int TextureHeight = 32;

    public enum Finish : byte { Surface, Metal, Wood, Mattress, Pillow }

    public readonly record struct Part(Vector3 Min, Vector3 Max, Finish Finish);

    public sealed class Model(params Part[] parts)
    {
        public ReadOnlySpan<Part> Parts => parts;
        public float Height { get; } = GetHeight(parts);
        public Vector2 HalfSize { get; } = GetHalfSize(parts);

        private static float GetHeight(Part[] source)
        {
            var height = 0f;
            foreach (var part in source) height = Math.Max(height, part.Max.Z);
            return height;
        }

        private static Vector2 GetHalfSize(Part[] source)
        {
            var size = Vector2.Zero;
            foreach (var part in source)
                size = Vector2.Max(size, Vector2.Max(Vector2.Abs(new Vector2(part.Min.X, part.Min.Y) - new Vector2(0.5f)),
                    Vector2.Abs(new Vector2(part.Max.X, part.Max.Y) - new Vector2(0.5f))));
            return size;
        }

        public bool TryPick(Vector3 origin, Vector3 ray, byte rotation, out float distance)
        {
            // Turn the ray into model space; only bits 0-1 describe the entity facing.
            origin -= new Vector3(0.5f, 0.5f, 0);
            for (var i = 0; i < (rotation & 3); i++)
            {
                origin = new Vector3(origin.Y, -origin.X, origin.Z);
                ray = new Vector3(ray.Y, -ray.X, ray.Z);
            }
            origin += new Vector3(0.5f, 0.5f, 0);
            distance = float.PositiveInfinity;
            foreach (var part in parts)
                if (CMUReconGeometry.IntersectBox(origin, ray, part.Min, part.Max, out var near, out _))
                    distance = Math.Min(distance, Math.Max(near, 0));
            return float.IsFinite(distance);
        }
    }

    private static Part Box(int x, int y, int z, int right, int back, int top, Finish finish = Finish.Surface) =>
        new(new Vector3(x, y, z) / 100f, new Vector3(right, back, top) / 100f, finish);

    private static Model Chair(bool wood, bool wings = false)
    {
        var frame = wood ? Finish.Wood : Finish.Metal;
        var parts = new List<Part>
        {
            Box(21, 23, 62, 79, 77, 72, wood ? Finish.Wood : Finish.Surface),
            Box(21, 69, 72, 79, 79, 129, wood ? Finish.Wood : Finish.Surface),
            Box(24, 26, 18, 32, 34, 62, frame), Box(68, 26, 18, 76, 34, 62, frame),
            Box(24, 66, 18, 32, 74, 62, frame), Box(68, 66, 18, 76, 74, 62, frame),
        };
        if (wings)
        {
            parts.Add(Box(15, 32, 79, 23, 75, 87, frame));
            parts.Add(Box(77, 32, 79, 85, 75, 87, frame));
        }
        return new Model(parts.ToArray());
    }

    private static Model Bench(bool right) => new(
        Box(0, 22, 62, 100, 78, 73), Box(0, 71, 73, 100, 82, 115),
        Box(14, 29, 18, 23, 71, 62, Finish.Metal), Box(77, 29, 18, 86, 71, 62, Finish.Metal),
        Box(right ? 92 : 0, 27, 73, right ? 100 : 8, 77, 89, Finish.Metal));

    private static Model Couch(int end) => new(
        Box(0, 15, 27, 100, 85, 55), Box(0, 16, 55, 100, 73, 70),
        Box(0, 73, 55, 100, 88, 120),
        // A middle section uses a rear plinth; end sections put an arm only at their exposed end.
        end == 0 ? Box(0, 75, 18, 100, 84, 27, Finish.Wood) :
            Box(end < 0 ? 0 : 89, 16, 55, end < 0 ? 11 : 100, 85, 88));

    private static Model Table(bool wood) => new(
        Box(2, 2, 85, 98, 98, 96, wood ? Finish.Wood : Finish.Surface),
        Box(8, 8, 18, 18, 18, 85, wood ? Finish.Wood : Finish.Metal),
        Box(82, 8, 18, 92, 18, 85, wood ? Finish.Wood : Finish.Metal),
        Box(8, 82, 18, 18, 92, 85, wood ? Finish.Wood : Finish.Metal),
        Box(82, 82, 18, 92, 92, 85, wood ? Finish.Wood : Finish.Metal));

    private static readonly Dictionary<CMUReconMaterial, Model> Models = new()
    {
        [CMUReconMaterial.Chair] = Chair(false),
        [CMUReconMaterial.WoodChair] = Chair(true),
        [CMUReconMaterial.WoodWingChair] = Chair(true, true),
        [CMUReconMaterial.FoldingChair] = new(
            Box(22, 24, 62, 78, 73, 69), Box(23, 73, 98, 77, 81, 123),
            Box(22, 25, 18, 28, 31, 62, Finish.Metal), Box(72, 25, 18, 78, 31, 62, Finish.Metal),
            Box(22, 73, 18, 28, 79, 110, Finish.Metal), Box(72, 73, 18, 78, 79, 110, Finish.Metal)),
        [CMUReconMaterial.OfficeChair] = new(
            Box(21, 22, 67, 79, 75, 78), Box(23, 70, 78, 77, 82, 137),
            Box(45, 44, 26, 55, 55, 67, Finish.Metal),
            Box(16, 45, 18, 84, 55, 27, Finish.Metal), Box(45, 17, 18, 55, 83, 27, Finish.Metal),
            Box(13, 31, 78, 21, 76, 91, Finish.Metal), Box(79, 31, 78, 87, 76, 91, Finish.Metal)),
        [CMUReconMaterial.Armchair] = new(
            Box(15, 19, 42, 85, 82, 66), Box(22, 22, 66, 78, 71, 80),
            Box(17, 70, 66, 83, 85, 147), Box(10, 23, 63, 23, 81, 98),
            Box(77, 23, 63, 90, 81, 98), Box(25, 30, 18, 75, 70, 42, Finish.Metal)),
        [CMUReconMaterial.Stool] = new(
            Box(22, 22, 67, 78, 78, 76),
            Box(25, 25, 18, 33, 33, 67, Finish.Metal), Box(67, 25, 18, 75, 33, 67, Finish.Metal),
            Box(25, 67, 18, 33, 75, 67, Finish.Metal), Box(67, 67, 18, 75, 75, 67, Finish.Metal)),
        [CMUReconMaterial.BenchLeft] = Bench(false),
        [CMUReconMaterial.BenchRight] = Bench(true),
        [CMUReconMaterial.Sofa] = new(
            Box(8, 15, 27, 92, 85, 55), Box(18, 16, 55, 82, 73, 70),
            Box(8, 73, 55, 92, 88, 120), Box(3, 16, 55, 18, 85, 88),
            Box(82, 16, 55, 97, 85, 88), Box(17, 25, 18, 83, 76, 27, Finish.Wood)),
        [CMUReconMaterial.Bed] = new(
            Box(14, 4, 48, 86, 96, 65, Finish.Mattress), Box(20, 71, 65, 80, 91, 73, Finish.Pillow),
            Box(12, 90, 18, 88, 98, 91, Finish.Metal),
            Box(16, 8, 18, 24, 16, 48, Finish.Metal), Box(76, 8, 18, 84, 16, 48, Finish.Metal),
            Box(16, 79, 18, 24, 87, 48, Finish.Metal), Box(76, 79, 18, 84, 87, 48, Finish.Metal)),
        [CMUReconMaterial.BunkBed] = new(
            Box(12, 6, 48, 88, 94, 65, Finish.Mattress), Box(12, 6, 157, 88, 94, 174, Finish.Mattress),
            Box(7, 3, 18, 14, 10, 212, Finish.Metal), Box(86, 3, 18, 93, 10, 212, Finish.Metal),
            Box(7, 90, 18, 14, 97, 212, Finish.Metal), Box(86, 90, 18, 93, 97, 212, Finish.Metal),
            Box(12, 91, 185, 88, 97, 205, Finish.Metal), Box(7, 8, 191, 13, 92, 201, Finish.Metal)),
        [CMUReconMaterial.Desk] = new(
            Box(1, 1, 85, 99, 99, 97), Box(6, 8, 18, 19, 92, 85, Finish.Metal),
            Box(81, 8, 18, 94, 92, 85, Finish.Metal), Box(19, 84, 45, 81, 92, 85, Finish.Metal)),
        [CMUReconMaterial.Shelf] = new(
            Box(6, 16, 40, 94, 84, 48, Finish.Metal), Box(6, 16, 101, 94, 84, 109, Finish.Metal),
            Box(6, 16, 162, 94, 84, 170, Finish.Metal),
            Box(6, 16, 18, 13, 23, 182, Finish.Metal), Box(87, 16, 18, 94, 23, 182, Finish.Metal),
            Box(6, 77, 18, 13, 84, 182, Finish.Metal), Box(87, 77, 18, 94, 84, 182, Finish.Metal)),
        [CMUReconMaterial.Bookcase] = new(
            Box(6, 75, 18, 94, 82, 210, Finish.Wood),
            Box(6, 18, 18, 14, 75, 210, Finish.Wood), Box(86, 18, 18, 94, 75, 210, Finish.Wood),
            Box(14, 18, 22, 86, 75, 30, Finish.Wood), Box(14, 18, 65, 86, 75, 73, Finish.Wood),
            Box(14, 18, 110, 86, 75, 118, Finish.Wood), Box(14, 18, 155, 86, 75, 163, Finish.Wood),
            Box(6, 18, 202, 94, 82, 210, Finish.Wood)),
        [CMUReconMaterial.Table] = Table(false),
        [CMUReconMaterial.WoodTable] = Table(true),
        [CMUReconMaterial.OperatingTable] = new(
            Box(14, 2, 88, 86, 75, 99), Box(14, 75, 99, 86, 98, 112),
            Box(38, 32, 29, 62, 68, 88, Finish.Metal), Box(24, 18, 18, 76, 82, 29, Finish.Metal)),
        [CMUReconMaterial.Counter] = new(
            Box(0, 0, 88, 100, 100, 101), Box(4, 8, 18, 96, 92, 88)),
        [CMUReconMaterial.CouchMiddle] = Couch(0),
        [CMUReconMaterial.CouchLeft] = Couch(-1),
        [CMUReconMaterial.CouchRight] = Couch(1),
    };

    public static bool TryGet(byte material, out Model model) => Models.TryGetValue((CMUReconMaterial) material, out model!);

    /// <summary>RGBA8: header (height, half width, half depth, part count), then (min, finish)/(max, unused).</summary>
    public static byte[] EncodeTexture()
    {
        var pixels = new byte[TextureWidth * TextureHeight * 4];
        foreach (var (material, model) in Models)
        {
            var at = ((int) material - FirstMaterial) * TextureWidth * 4;
            pixels[at++] = Encode(model.Height);
            pixels[at++] = Encode(model.HalfSize.X);
            pixels[at++] = Encode(model.HalfSize.Y);
            pixels[at++] = (byte) model.Parts.Length;
            foreach (var part in model.Parts)
            {
                pixels[at++] = Encode(part.Min.X); pixels[at++] = Encode(part.Min.Y); pixels[at++] = Encode(part.Min.Z);
                pixels[at++] = (byte) part.Finish;
                pixels[at++] = Encode(part.Max.X); pixels[at++] = Encode(part.Max.Y); pixels[at++] = Encode(part.Max.Z);
                pixels[at++] = 0;
            }
        }
        return pixels;
    }

    private static byte Encode(float value) => checked((byte) MathF.Round(value * 100));
}
