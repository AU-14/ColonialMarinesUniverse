namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>Lossless per-chunk palette. Repeated floor/wall cells travel once plus byte indices.</summary>
public static class CMUReconChunkEncoding
{
    private const int Count = CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize;

    public static CMUReconChunk Pack(CMUReconChunk chunk)
    {
        if (chunk.Empty || chunk.Cells.Length != Count || chunk.Appearance?.Length != Count || chunk.Directions?.Length != Count)
            return chunk;
        var palette = new Dictionary<(byte Cell, uint Style, byte Direction), byte>();
        var indices = new byte[Count];
        for (var i = 0; i < Count; i++)
        {
            var value = (chunk.Cells[i], chunk.Appearance[i], chunk.Directions[i]);
            if (!palette.TryGetValue(value, out var id))
            {
                // Fall back to raw cells when the palette would be larger than the original.
                if (1 + (palette.Count + 1) * 6 + Count >= Count * 6) return chunk;
                palette.Add(value, id = (byte) palette.Count);
            }
            indices[i] = id;
        }
        var packed = new byte[1 + palette.Count * 6 + Count];
        packed[0] = (byte) palette.Count;
        foreach (var (value, id) in palette)
        {
            var at = 1 + id * 6;
            packed[at] = value.Cell;
            for (var b = 0; b < 4; b++) packed[at + 1 + b] = (byte) (value.Style >> (b * 8));
            packed[at + 5] = value.Direction;
        }
        indices.CopyTo(packed, 1 + palette.Count * 6);
        return chunk with { Cells = [], Appearance = null, Directions = null, Packed = packed };
    }

    public static bool ValidPacked(byte[] packed)
    {
        if (packed.Length < 1 || packed[0] == 0 || packed.Length != 1 + packed[0] * 6 + Count) return false;
        for (var i = 1 + packed[0] * 6; i < packed.Length; i++)
            if (packed[i] >= packed[0]) return false;
        return true;
    }

    public static bool TryUnpack(CMUReconChunk chunk, out CMUReconChunk result)
    {
        result = chunk;
        if (chunk.Packed is not { } packed) return true;
        if (chunk.Empty || !ValidPacked(packed)) return false;
        var cells = new byte[Count];
        var styles = new uint[Count];
        var directions = new byte[Count];
        for (var i = 0; i < Count; i++)
        {
            var at = 1 + packed[1 + packed[0] * 6 + i] * 6;
            cells[i] = packed[at];
            for (var b = 0; b < 4; b++) styles[i] |= (uint) packed[at + 1 + b] << (b * 8);
            directions[i] = packed[at + 5];
        }
        result = chunk with { Cells = cells, Appearance = styles, Directions = directions, Packed = null };
        return true;
    }

    public static int EstimatedBytes(CMUReconChunk chunk) => chunk.Empty ? 32 : (chunk.Packed?.Length ?? Count * 6) + 64;
}
