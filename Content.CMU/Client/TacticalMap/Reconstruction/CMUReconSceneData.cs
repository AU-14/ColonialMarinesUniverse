using System.Linq;
using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

/// <summary>CPU survey assembly, also used while a cached scene remains on screen.</summary>
public static class CMUReconSceneData
{
    public static bool Initialize(CMUReconSnapshotMessage scene)
    {
        if (!CMUReconGeometry.ValidDimensions(scene.Width, scene.Height, scene.Levels)) return false;
        var count = scene.Width * scene.Height * scene.Levels;
        if (scene.Cells.Length != 0 && scene.Cells.Length != count ||
            scene.Appearance.Length != 0 && scene.Appearance.Length != count ||
            scene.Directions.Length != 0 && scene.Directions.Length != count) return false;
        if (scene.Cells.Length == 0) scene.Cells = new byte[count];
        if (scene.Appearance.Length == 0) scene.Appearance = new uint[count];
        if (scene.Directions.Length == 0) scene.Directions = new byte[count];
        var chunks = count / (CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize);
        if (scene.Revisions.Length != chunks) scene.Revisions = new int[chunks];
        if (scene.EmptyChunks.Length == (chunks + 7) / 8)
            for (var i = 0; i < chunks; i++)
                if ((scene.EmptyChunks[i / 8] & (1 << (i % 8))) != 0) scene.Revisions[i] = 1;
        return true;
    }

    public static bool ValidChunk(CMUReconSnapshotMessage scene, CMUReconChunk chunk)
    {
        const int size = CMUReconGeometry.ChunkSize;
        return chunk.Level >= 0 && chunk.Level < scene.Levels && chunk.X >= 0 && chunk.Y >= 0 &&
            chunk.X < scene.Width / size && chunk.Y < scene.Height / size &&
            (chunk.Packed is { } packed ? !chunk.Empty && CMUReconChunkEncoding.ValidPacked(packed) :
            (chunk.Empty ? chunk.Cells.Length == 0 : chunk.Cells.Length == size * size) &&
            (chunk.Appearance == null || chunk.Appearance.Length == size * size) &&
            (chunk.Directions == null || chunk.Directions.Length == size * size));
    }

    public static void Apply(CMUReconSnapshotMessage scene, CMUReconPatchMessage patch)
    {
        if (scene.Generation != patch.Generation) return;
        if (patch.OrdersChanged) scene.Orders = patch.Orders;
        scene.CanOrder = patch.CanOrder;
        scene.Layer = patch.Layer;
        scene.AvailableLayers = patch.AvailableLayers;
        scene.LoadedChunks = patch.LoadedChunks;
        scene.TotalChunks = patch.TotalChunks;
        if (patch.Surfaces.Length > 0)
            scene.Surfaces = scene.Surfaces.Concat(patch.Surfaces).DistinctBy(s => s.Id).ToArray();
        foreach (var encoded in patch.Chunks)
        {
            if (!ValidChunk(scene, encoded) || !CMUReconChunkEncoding.TryUnpack(encoded, out var chunk)) continue;
            const int size = CMUReconGeometry.ChunkSize;
            var across = scene.Width / size;
            var id = chunk.Level * across * (scene.Height / size) + chunk.Y * across + chunk.X;
            if (scene.Revisions.Length > id) scene.Revisions[id] = chunk.Revision;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var source = y * size + x;
                var index = CMUReconGeometry.Index(chunk.X * size + x, chunk.Y * size + y, chunk.Level, scene.Width, scene.Height);
                scene.Cells[index] = chunk.Empty ? (byte) 0 : chunk.Cells[source];
                scene.Appearance[index] = chunk.Appearance?[source] ?? 0;
                scene.Directions[index] = chunk.Directions?[source] ?? 0;
            }
        }
    }
}
