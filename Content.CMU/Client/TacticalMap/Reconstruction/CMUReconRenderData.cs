using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

/// <summary>Owned rendering resources that travel with a cached survey instead of its window.</summary>
public sealed class CMUReconRenderData : IDisposable
{
    internal ShaderInstance? Shader;
    internal OwnedTexture? Terrain;
    internal OwnedTexture? FurnitureModels;
    internal OwnedTexture? Appearance;
    internal OwnedTexture? Occupancy;
    internal Rgba32[] ChunkPixels = [];
    internal IRenderTexture? SurfaceAtlas;
    internal readonly Dictionary<ushort, (Texture[] Textures, UIBox2? Region, Color Tint)> SurfaceTextures = new();
    internal readonly List<ushort> NewSurfaces = new();
    internal readonly Queue<CMUReconSurface> PendingSurfaces = new();
    internal readonly Queue<(int X, int Y, int Level)> PendingUploads = new();
    internal readonly HashSet<(int X, int Y, int Level)> QueuedUploads = new();
    internal bool ClearSurfaces;
    internal IRenderTexture? Target;
    public bool Disposed { get; private set; }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Target?.Dispose();
        Terrain?.Dispose();
        Appearance?.Dispose();
        Occupancy?.Dispose();
        SurfaceAtlas?.Dispose();
        FurnitureModels?.Dispose();
        Shader?.Dispose();
        Target = SurfaceAtlas = null;
        Terrain = Appearance = Occupancy = FurnitureModels = null;
        Shader = null;
        ChunkPixels = [];
        SurfaceTextures.Clear();
        NewSurfaces.Clear();
        PendingSurfaces.Clear();
        PendingUploads.Clear();
        QueuedUploads.Clear();
    }
}
