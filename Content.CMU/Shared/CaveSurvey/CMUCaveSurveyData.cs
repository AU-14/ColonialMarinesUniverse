using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.Shared.CMU14.CaveSurvey;

public readonly record struct CMUCaveSurveyCell(long Revision, CMUReconMaterial Material);
public readonly record struct CMUCaveSurveySample(Vector2i Tile, int Depth, CMUReconMaterial Material);

/// <summary>
/// Sparse round-local observations. Blocks are immutable after publication, so maps share their frozen
/// contents until an explorer revisits them. Revision zero means unknown; observed empty space is data.
/// </summary>
public sealed class CMUCaveSurveyData
{
    public const int BlockSize = 4;
    public const int MaxBlocks = 8192;
    private readonly Dictionary<(Vector2i Block, int Depth), CMUCaveSurveyCell[]> _blocks;
    public int Version { get; private set; }
    public int BlockCount => _blocks.Count;
    public bool Full => BlockCount >= MaxBlocks;

    public CMUCaveSurveyData() => _blocks = new();

    private CMUCaveSurveyData(CMUCaveSurveyData source)
    {
        _blocks = new(source._blocks);
        Version = source.Version;
    }

    public CMUCaveSurveyData Snapshot() => new(this);

    public static int FloorDiv(int value, int divisor) => (int) Math.Floor((double) value / divisor);

    public CMUCaveSurveyCell Get(Vector2i tile, int depth)
    {
        var block = new Vector2i(FloorDiv(tile.X, BlockSize), FloorDiv(tile.Y, BlockSize));
        return _blocks.TryGetValue((block, depth), out var cells)
            ? cells[(tile.Y - block.Y * BlockSize) * BlockSize + tile.X - block.X * BlockSize]
            : default;
    }

    public void Record(IReadOnlyList<CMUCaveSurveySample> samples, long revision)
    {
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        var changed = new Dictionary<(Vector2i Block, int Depth), CMUCaveSurveyCell[]>();
        var added = 0;
        foreach (var sample in samples)
        {
            var block = new Vector2i(FloorDiv(sample.Tile.X, BlockSize), FloorDiv(sample.Tile.Y, BlockSize));
            var key = (block, sample.Depth);
            var index = (sample.Tile.Y - block.Y * BlockSize) * BlockSize + sample.Tile.X - block.X * BlockSize;
            if (!changed.TryGetValue(key, out var cells))
            {
                if (!_blocks.TryGetValue(key, out var old))
                {
                    if (_blocks.Count + added >= MaxBlocks) continue;
                    added++;
                }
                if (old != null && old[index].Revision >= revision)
                    continue;
                cells = old == null ? new CMUCaveSurveyCell[16] : (CMUCaveSurveyCell[]) old.Clone();
                changed.Add(key, cells);
            }
            if (cells[index].Revision < revision)
                cells[index] = new(revision, sample.Material);
        }
        foreach (var (key, cells) in changed) _blocks[key] = cells;
        if (changed.Count > 0) Version++;
    }

    /// <summary>Never retimestamp copied knowledge. Reading order must not change the resulting terrain.</summary>
    public void Merge(CMUCaveSurveyData source)
    {
        var changed = false;
        foreach (var (key, incoming) in source._blocks)
        {
            if (!_blocks.TryGetValue(key, out var current))
            {
                if (Full) continue;
                _blocks.Add(key, incoming);
                changed = true;
                continue;
            }
            if (ReferenceEquals(current, incoming)) continue;
            CMUCaveSurveyCell[]? replacement = null;
            for (var i = 0; i < 16; i++)
            {
                if (incoming[i].Revision <= current[i].Revision) continue;
                replacement ??= (CMUCaveSurveyCell[]) current.Clone();
                replacement[i] = incoming[i];
            }
            if (replacement == null) continue;
            _blocks[key] = replacement;
            changed = true;
        }
        if (changed) Version++;
    }

    public bool TryFirst(out Vector2i tile, out int depth)
    {
        foreach (var (key, _) in _blocks)
        {
            tile = key.Block * BlockSize;
            depth = key.Depth;
            return true;
        }
        tile = default;
        depth = default;
        return false;
    }

    public bool TryGetBounds(out Vector2i min, out Vector2i max)
    {
        min = new Vector2i(int.MaxValue, int.MaxValue);
        max = new Vector2i(int.MinValue, int.MinValue);
        foreach (var (block, _) in _blocks.Keys)
        {
            min = new Vector2i(Math.Min(min.X, block.X), Math.Min(min.Y, block.Y));
            max = new Vector2i(Math.Max(max.X, block.X), Math.Max(max.Y, block.Y));
        }
        if (_blocks.Count == 0) return false;
        min *= BlockSize;
        max = (max + Vector2i.One) * BlockSize;
        return true;
    }

    /// <summary>Visits only recorded blocks, including when a large view spans mostly unknown terrain.</summary>
    public IEnumerable<CMUCaveSurveySample> Section(Vector2i origin, Vector2i size, int minDepth, int levels)
    {
        foreach (var ((block, depth), cells) in _blocks)
        {
            var start = block * BlockSize;
            if (depth < minDepth || depth >= minDepth + levels ||
                start.X >= origin.X + size.X || start.Y >= origin.Y + size.Y ||
                start.X + BlockSize <= origin.X || start.Y + BlockSize <= origin.Y) continue;
            for (var i = 0; i < 16; i++)
            {
                if (cells[i].Revision == 0) continue;
                var tile = start + new Vector2i(i % BlockSize, i / BlockSize);
                if (tile.X < origin.X || tile.Y < origin.Y || tile.X >= origin.X + size.X || tile.Y >= origin.Y + size.Y) continue;
                yield return new(tile, depth, cells[i].Material);
            }
        }
    }
}
