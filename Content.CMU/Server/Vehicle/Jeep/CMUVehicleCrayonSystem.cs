using System.Numerics;
using Content.Shared.CMU14.Vehicle.Jeep;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Server.CMU14.Vehicle.Jeep;

public sealed class CMUVehicleCrayonSystem : SharedCMUVehicleCrayonSystem
{
    [Dependency] private IResourceManager _resources = default!;

    private readonly Dictionary<ResPath, Image<Rgba32>?> _maps = new();

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var map in _maps.Values)
        {
            map?.Dispose();
        }

        _maps.Clear();
    }

    public override bool TryGetSurface(ResPath map, Direction direction, int x, int y, out Vector3 voxel, out bool front, out bool overRiders)
    {
        voxel = default;
        front = false;
        overRiders = false;
        if (x < 0 || y < 0 || x >= FrameSize || y >= FrameSize || GetMap(map) is not { } image)
            return false;

        var (px, py) = MapPixel(direction, x, y);
        var pixel = image[px, py];
        return DecodeSurface(pixel.R, pixel.G, pixel.B, pixel.A, out voxel, out front, out overRiders);
    }

    private Image<Rgba32>? GetMap(ResPath path)
    {
        if (_maps.TryGetValue(path, out var map))
            return map;

        if (_resources.TryContentFileRead(path, out var stream))
        {
            using (stream)
            {
                map = Image.Load<Rgba32>(stream);
            }
        }

        _maps[path] = map;
        return map;
    }
}
