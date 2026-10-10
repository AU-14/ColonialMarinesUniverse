using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>
/// Bounded RGBA8 data for the content ray shader. Box geometry is quantized once and the same
/// decoded geometry drives grid membership and CPU picking. A source entity is admitted atomically.
/// </summary>
public sealed class CMU3DSceneEncoding
{
    public const int GridSize = 16;
    public const float GridMin = -8;
    public const int MaxBoxes = 8192;
    public const int MaxSnapshotBoxes = ExtendedMaxBoxes;
    // Cells reference all admitted geometry, including dense multi-part source entities.
    public const int MaxCellBoxes = MaxSnapshotBoxes;
    public const int MaxGridReferences = 4 * 1024 * 1024;
    public const int TextureWidth = 256;
    public const int GridTextureWidth = 2048;
    public const int BoxTexels = 6;
    public const int BoxTextureHeight = MaxBoxes * BoxTexels / TextureWidth;
    public const int CellDescriptorTexels = 2;
    public const float CoordinateScale = 1024;
    public const float HalfSizeScale = 2048;
    public const float RotationScale = 32767;

    // References hold 17-bit IDs; align geometry to whole 256-texel rows.
    public const int ExtendedMaxBoxes = 130560;
    public readonly Rgba32[] BoxPixels;
    public Rgba32[] GridPixels { get; private set; }
    private int[] _cellIds = [];
    private int[] _nextBox = [];
    private int[] _groupCounts = [];
    private readonly List<int> _groupStarts = [];
    private readonly Dictionary<EntityUid, (int First, int Last)> _sourceGroups = [];
    private readonly List<CMU3DSceneBox> _decoded = [];
    private readonly record struct Footprint(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ);
    private readonly List<Footprint> _footprints = [];
    private readonly List<Footprint> _acceptedFootprints = [];
    private readonly int[] _cellCursors;
    private readonly byte[] _heightMin;
    private readonly byte[] _heightMax;
    private readonly int[] _cellCounts;
    private readonly int[] _cellOffsets;
    public int SpatialSize { get; }
    public int SpatialDepth { get; }
    public const float SpatialZMin = -80;
    public float SpatialZStep => 160f / SpatialDepth;
    public float SpatialMin => -SpatialSize / 2f;
    public int Capacity { get; }
    public int BoxRows => Capacity * BoxTexels / TextureWidth;
    public int GridRows => GridPixels.Length / GridTextureWidth;
    public int GridReferences { get; private set; }
    public bool ReferenceBudgetExceeded { get; private set; }

    public CMU3DSceneEncoding() : this(false) { }

    public CMU3DSceneEncoding(bool extended)
    {
        SpatialSize = extended ? 64 : GridSize;
        // Quantized centers and tilted half-extents remain inside [-80,80].
        // Two-unit slices avoid scanning every floor's parts in each XY cell.
        SpatialDepth = extended ? 80 : 1;
        Capacity = extended ? ExtendedMaxBoxes : MaxBoxes;
        BoxPixels = new Rgba32[TextureWidth * BoxRows];
        GridPixels = new Rgba32[Math.Max(GridTextureWidth, SpatialSize * SpatialSize * SpatialDepth * CellDescriptorTexels)];
        _cellCounts = new int[SpatialSize * SpatialSize * SpatialDepth];
        _cellOffsets = new int[_cellCounts.Length];
        _cellCursors = new int[_cellCounts.Length];
        _heightMin = new byte[Capacity];
        _heightMax = new byte[Capacity];
    }
    private readonly List<CMU3DSceneBox> _boxes = [];

    public IReadOnlyList<CMU3DSceneBox> Boxes => _boxes;
    public int AcceptedBoxes => _boxes.Count;
    public int OmittedBoxes { get; private set; }
    public float MinZ { get; private set; }
    public float MaxZ { get; private set; }

    public void Build(IReadOnlyList<CMU3DSceneBox> boxes)
    {
        foreach (var step in BuildSteps(boxes)) { }
    }

    /// <summary>
    /// Builds an inactive snapshot in bounded batches. The caller must keep the input
    /// stable and must not publish this encoder until enumeration completes.
    /// </summary>
    public IEnumerable<bool> BuildSteps(IReadOnlyList<CMU3DSceneBox> boxes, Func<bool>? shouldYield = null)
    {
        _boxes.Clear();
        Array.Clear(_cellCounts);
        Array.Clear(_cellOffsets);
        Array.Clear(GridPixels);
        GridReferences = 0;
        ReferenceBudgetExceeded = false;
        OmittedBoxes = 0;
        MinZ = 0;
        MaxZ = 1;
        if (boxes.Count > MaxSnapshotBoxes)
        {
            OmittedBoxes = boxes.Count;
            yield break;
        }

        // Link source groups through reusable input indices. Atomic admission does not
        // require allocating a list for every floor tile/entity whenever the player walks.
        if (_nextBox.Length < boxes.Count)
        {
            var inputCapacity = Math.Max(256, _nextBox.Length);
            while (inputCapacity < boxes.Count) inputCapacity *= 2;
            _nextBox = new int[inputCapacity];
            _groupCounts = new int[inputCapacity];
        }
        _sourceGroups.Clear();
        _groupStarts.Clear();
        for (var i = 0; i < boxes.Count; i++)
        {
            if (i % 512 == 0 && shouldYield?.Invoke() == true) yield return true;
            _nextBox[i] = -1;
            _groupCounts[i] = 1;
            if (boxes[i].Source is not { } source)
            {
                _groupStarts.Add(i);
                continue;
            }
            if (_sourceGroups.TryGetValue(source, out var previous))
            {
                _nextBox[previous.Last] = i;
                _groupCounts[previous.First]++;
                _sourceGroups[source] = (previous.First, i);
            }
            else
            {
                _sourceGroups[source] = (i, i);
                _groupStarts.Add(i);
            }
        }

        var decoded = _decoded;
        var footprints = _footprints;
        var acceptedFootprints = _acceptedFootprints;
        acceptedFootprints.Clear();
        var examined = 0;
        foreach (var first in _groupStarts)
        {
            if (++examined % 128 == 0 && shouldYield?.Invoke() == true) yield return true;
            var count = _groupCounts[first];
            if (count + _boxes.Count > Capacity)
            {
                OmittedBoxes += count;
                continue;
            }
            decoded.Clear();
            footprints.Clear();
            var accepted = true;
            var references = 0L;
            for (var input = first; input >= 0; input = _nextBox[input])
            {
                if (++examined % 128 == 0 && shouldYield?.Invoke() == true) yield return true;
                var original = boxes[input];
                if (!Valid(original))
                {
                    accepted = false;
                    break;
                }
                var box = Quantize(original);
                if (box.HalfSize.X <= 0 || box.HalfSize.Y <= 0 || box.HalfSize.Z <= 0)
                {
                    accepted = false;
                    break;
                }
                decoded.Add(box);
                var half = box.AxisAlignedHalfSize;
                var extent = new Vector2(half.X, half.Y);
                var center = new Vector2(box.Center.X, box.Center.Y);
                // Include boundary faces despite floating-point slab tolerances.
                var low = center - extent - new Vector2(0.002f);
                var high = center + extent + new Vector2(0.002f);
                var minX = Math.Max(0, (int) MathF.Floor(low.X - SpatialMin));
                var minY = Math.Max(0, (int) MathF.Floor(low.Y - SpatialMin));
                var maxX = Math.Min(SpatialSize - 1, (int) MathF.Floor(high.X - SpatialMin));
                var maxY = Math.Min(SpatialSize - 1, (int) MathF.Floor(high.Y - SpatialMin));
                var minZ = Math.Max(0, (int) MathF.Floor((box.Center.Z - half.Z - .002f - SpatialZMin) / SpatialZStep));
                var maxZ = Math.Min(SpatialDepth - 1, (int) MathF.Floor((box.Center.Z + half.Z + .002f - SpatialZMin) / SpatialZStep));
                footprints.Add(new Footprint(minX, minY, minZ, maxX, maxY, maxZ));
                references += (long) Math.Max(0, maxX - minX + 1) * Math.Max(0, maxY - minY + 1) * Math.Max(0, maxZ - minZ + 1);
            }
            if (!accepted)
            {
                OmittedBoxes += count;
                continue;
            }
            if (GridReferences + references > MaxGridReferences)
            {
                // An exceptional memory limit invalidates the entire snapshot, never a
                // silently incomplete cell. Typical scenes need far fewer references.
                ReferenceBudgetExceeded = true;
                _boxes.Clear();
                Array.Clear(_cellCounts);
                GridReferences = 0;
                OmittedBoxes = boxes.Count;
                MinZ = 0;
                MaxZ = 1;
                yield break;
            }
            GridReferences += (int) references;
            var originalIndex = first;
            for (var i = 0; i < decoded.Count; i++)
            {
                if (i % 128 == 0 && shouldYield?.Invoke() == true) yield return true;
                var box = decoded[i];
                var id = _boxes.Count;
                _boxes.Add(box);
                // Re-encoding the decoded angle would round cosine/sine twice and
                // make CPU picking differ from the shader. Encode the original once.
                WriteBox(id, boxes[originalIndex]);
                originalIndex = _nextBox[originalIndex];
                var height = box.AxisAlignedHalfSize.Z;
                var lowZ = box.Center.Z - height;
                var highZ = box.Center.Z + height;
                MinZ = Math.Min(MinZ, lowZ);
                MaxZ = Math.Max(MaxZ, highZ);
                // Conservative two-unit bounds fit every quantized/tilted box in seven bits.
                // A reference can reject other floors without fetching the model's six texels.
                _heightMin[id] = (byte) Math.Clamp((int) MathF.Floor((lowZ - .002f) / 2) + 64, 0, 127);
                _heightMax[id] = (byte) Math.Clamp((int) MathF.Ceiling((highZ + .002f) / 2) + 64, 0, 127);
                var footprint = footprints[i];
                acceptedFootprints.Add(footprint);
                for (var z = footprint.MinZ; z <= footprint.MaxZ; z++)
                for (var y = footprint.MinY; y <= footprint.MaxY; y++)
                for (var x = footprint.MinX; x <= footprint.MaxX; x++)
                    _cellCounts[(z * SpatialSize + y) * SpatialSize + x]++;
            }
        }

        var offset = 0;
        for (var cell = 0; cell < _cellCounts.Length; cell++)
        {
            if (cell % 4096 == 0 && shouldYield?.Invoke() == true) yield return true;
            _cellOffsets[cell] = offset;
            offset += _cellCounts[cell];
        }
        if (_cellIds.Length < GridReferences)
            _cellIds = new int[GridReferences];
        var cursors = _cellCursors;
        Array.Clear(cursors);
        for (var i = 0; i < acceptedFootprints.Count; i++)
        {
            if (i % 128 == 0 && shouldYield?.Invoke() == true) yield return true;
            var footprint = acceptedFootprints[i];
            for (var z = footprint.MinZ; z <= footprint.MaxZ; z++)
            for (var y = footprint.MinY; y <= footprint.MaxY; y++)
            for (var x = footprint.MinX; x <= footprint.MaxX; x++)
            {
                var cell = (z * SpatialSize + y) * SpatialSize + x;
                _cellIds[_cellOffsets[cell] + cursors[cell]++] = i + 1;
            }
        }
        var descriptors = _cellCounts.Length * CellDescriptorTexels;
        var texels = descriptors + GridReferences;
        var length = GridPixels.Length;
        while (length < texels)
            length *= 2;
        if (length != GridPixels.Length)
            GridPixels = new Rgba32[length];
        for (var cell = 0; cell < _cellCounts.Length; cell++)
        {
            if (cell % 4096 == 0 && shouldYield?.Invoke() == true) yield return true;
            // RGB stores 24-bit reference offsets and counts.
            // The 4M reference budget remains exactly representable by shader floats.
            var start = _cellOffsets[cell];
            GridPixels[cell * CellDescriptorTexels] = new Rgba32((byte) start, (byte) (start >> 8), (byte) (start >> 16), 0);
            var count = _cellCounts[cell];
            GridPixels[cell * CellDescriptorTexels + 1] = new Rgba32((byte) count, (byte) (count >> 8), (byte) (count >> 16), 0);
        }
        for (var i = 0; i < GridReferences; i++)
        {
            if (i % 4096 == 0 && shouldYield?.Invoke() == true) yield return true;
            var id = _cellIds[i];
            // B's high bit extends the ID without adding another texture fetch per reference.
            var heightAndId = (byte) (_heightMin[id - 1] | ((id >> 16) << 7));
            GridPixels[descriptors + i] = new Rgba32((byte) id, (byte) (id >> 8), heightAndId, _heightMax[id - 1]);
        }
    }

    // The content IL verifier rejects methods that return ref-like types such as ReadOnlySpan.
    public ReadOnlyMemory<int> Cell(int x, int y) => Cell(x, y, SpatialDepth / 2);

    public ReadOnlyMemory<int> Cell(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 || x >= SpatialSize || y >= SpatialSize || z >= SpatialDepth)
            return ReadOnlyMemory<int>.Empty;
        var cell = (z * SpatialSize + y) * SpatialSize + x;
        return new ReadOnlyMemory<int>(_cellIds, _cellOffsets[cell], _cellCounts[cell]);
    }

    public bool TryPick(Vector3 origin, Vector3 direction, out CMU3DSceneHit hit, CMU3DSceneSurfaces? surfaces = null)
    {
        hit = default;
        if (!Finite(origin) || !Finite(direction) || direction.LengthSquared() < 0.000001f)
            return false;
        direction = Vector3.Normalize(direction);
        var start = 0f;
        var end = float.PositiveInfinity;
        if (!ClipRay(origin.X, direction.X, SpatialMin, SpatialMin + SpatialSize, ref start, ref end) ||
            !ClipRay(origin.Y, direction.Y, SpatialMin, SpatialMin + SpatialSize, ref start, ref end) ||
            !ClipRay(origin.Z, direction.Z, MinZ - .002f, MaxZ + .002f, ref start, ref end))
            return false;
        var entry = origin + direction * start;
        var x = Math.Clamp((int) MathF.Floor(entry.X - SpatialMin), 0, SpatialSize - 1);
        var y = Math.Clamp((int) MathF.Floor(entry.Y - SpatialMin), 0, SpatialSize - 1);
        var z = Math.Clamp((int) MathF.Floor((entry.Z - SpatialZMin) / SpatialZStep), 0, SpatialDepth - 1);
        var stepX = Math.Sign(direction.X);
        var stepY = Math.Sign(direction.Y);
        var stepZ = Math.Sign(direction.Z);
        // Walk the same packed cells as the shader. UI, weapons and hover queries
        // otherwise intersect every detail on every replicated floor for each aim request.
        for (var step = 0; step <= SpatialSize * 2 + SpatialDepth && start <= end; step++)
        {
            var nextX = stepX == 0 ? float.PositiveInfinity :
                (x + SpatialMin + (stepX > 0 ? 1 : 0) - origin.X) / direction.X;
            var nextY = stepY == 0 ? float.PositiveInfinity :
                (y + SpatialMin + (stepY > 0 ? 1 : 0) - origin.Y) / direction.Y;
            var nextZ = stepZ == 0 ? float.PositiveInfinity :
                (SpatialZMin + (z + (stepZ > 0 ? 1 : 0)) * SpatialZStep - origin.Z) / direction.Z;
            var crossing = Math.Min(nextX, Math.Min(nextY, nextZ));
            var cellEnd = Math.Min(end, crossing);
            var cell = (z * SpatialSize + y) * SpatialSize + x;
            var best = float.PositiveInfinity;
            var bestIndex = -1;
            for (var reference = _cellOffsets[cell]; reference < _cellOffsets[cell] + _cellCounts[cell]; reference++)
            {
                var index = _cellIds[reference] - 1;
                var box = _boxes[index];
                if (!Intersect(box, origin, direction, out var candidate, surfaces) || candidate > best ||
                    candidate < start - .001f || candidate > cellEnd + .001f)
                    continue;
                var point = origin + direction * candidate;
                if (point.X < SpatialMin || point.Y < SpatialMin || point.X > SpatialMin + SpatialSize || point.Y > SpatialMin + SpatialSize)
                    continue;
                best = candidate;
                bestIndex = index;
                hit = new CMU3DSceneHit(point, candidate, box.Source, index);
            }
            if (bestIndex >= 0)
                return true;
            if (cellEnd >= end)
                break;
            if (nextX <= crossing) x += stepX;
            if (nextY <= crossing) y += stepY;
            if (nextZ <= crossing) z += stepZ;
            if (x < 0 || y < 0 || z < 0 || x >= SpatialSize || y >= SpatialSize || z >= SpatialDepth)
                break;
            start = cellEnd;
        }
        return false;
    }

    private static bool ClipRay(float origin, float direction, float min, float max, ref float start, ref float end)
    {
        if (direction == 0)
            return origin >= min && origin <= max;
        var a = (min - origin) / direction;
        var b = (max - origin) / direction;
        start = Math.Max(start, Math.Min(a, b));
        end = Math.Min(end, Math.Max(a, b));
        return end >= start;
    }

    public static bool Intersect(CMU3DSceneBox box, Vector3 origin, Vector3 direction, out float distance, CMU3DSceneSurfaces? surfaces = null)
    {
        distance = 0;
        if (box.Color.A <= 0)
            return false;

        var c = MathF.Cos(box.Yaw);
        var s = MathF.Sin(box.Yaw);
        var relative = origin - box.Center;
        var localOrigin = new Vector3(c * relative.X + s * relative.Y, -s * relative.X + c * relative.Y, relative.Z);
        var localRay = new Vector3(c * direction.X + s * direction.Y, -s * direction.X + c * direction.Y, direction.Z);
        var cp = MathF.Cos(box.Pitch);
        var sp = MathF.Sin(box.Pitch);
        localOrigin = new Vector3(cp * localOrigin.X + sp * localOrigin.Z, localOrigin.Y, -sp * localOrigin.X + cp * localOrigin.Z);
        localRay = new Vector3(cp * localRay.X + sp * localRay.Z, localRay.Y, -sp * localRay.X + cp * localRay.Z);
        if (box.Shape == CMU3DPartShape.Foliage)
            return CMU3DFoliage.Intersect(localOrigin / (box.HalfSize * 2), localRay / (box.HalfSize * 2), out distance);
        if (box.Shape is CMU3DPartShape.Ellipsoid or CMU3DPartShape.SlantedX or CMU3DPartShape.SlantedXReverse or CMU3DPartShape.SlantedY or CMU3DPartShape.SlantedYReverse)
        {
            var o = localOrigin / box.HalfSize;
            var d = localRay / box.HalfSize;
            if (box.Shape != CMU3DPartShape.Ellipsoid)
            {
                // Inverse of the export/browser ellipsoid shear, still inside the authored bounds.
                var reverse = box.Shape is CMU3DPartShape.SlantedXReverse or CMU3DPartShape.SlantedYReverse;
                var shear = reverse ? -.85f : .85f;
                var scale = MathF.Sqrt(1 - shear * shear);
                if (box.Shape is CMU3DPartShape.SlantedX or CMU3DPartShape.SlantedXReverse)
                {
                    o.X = (o.X - shear * o.Z) / scale;
                    d.X = (d.X - shear * d.Z) / scale;
                }
                else
                {
                    o.Y = (o.Y - shear * o.Z) / scale;
                    d.Y = (d.Y - shear * d.Z) / scale;
                }
            }
            var a = Vector3.Dot(d, d);
            var b = Vector3.Dot(o, d);
            var discriminant = b * b - a * (Vector3.Dot(o, o) - 1);
            if (a <= 0 || discriminant < 0)
                return false;
            var root = MathF.Sqrt(discriminant);
            var near = (-b - root) / a;
            var far = (-b + root) / a;
            distance = near >= 0.0001f ? near : far;
            return distance >= 0.0001f && float.IsFinite(distance);
        }
        var enter = float.NegativeInfinity;
        var exit = float.PositiveInfinity;
        if (box.Shape is CMU3DPartShape.CylinderX or CMU3DPartShape.CylinderY or CMU3DPartShape.CylinderZ)
        {
            var axis = (int) box.Shape - (int) CMU3DPartShape.CylinderX;
            var o = localOrigin / box.HalfSize;
            var d = localRay / box.HalfSize;
            var axialOrigin = CMU3DModelFace.Coordinate(o, axis);
            var axialRay = CMU3DModelFace.Coordinate(d, axis);
            var radialO = axis == 0 ? new Vector2(o.Y, o.Z) : axis == 1 ? new Vector2(o.Z, o.X) : new Vector2(o.X, o.Y);
            var radialD = axis == 0 ? new Vector2(d.Y, d.Z) : axis == 1 ? new Vector2(d.Z, d.X) : new Vector2(d.X, d.Y);
            var a = Vector2.Dot(radialD, radialD);
            var b = Vector2.Dot(radialO, radialD);
            var radialDistance = Vector2.Dot(radialO, radialO) - 1;
            if (a < 0.000000000001f)
            {
                if (radialDistance > 0)
                    return false;
            }
            else
            {
                var discriminant = b * b - a * radialDistance;
                if (discriminant < 0)
                    return false;
                var root = MathF.Sqrt(discriminant);
                enter = (-b - root) / a;
                exit = (-b + root) / a;
            }
            if (MathF.Abs(axialRay) < 0.000001f)
            {
                if (MathF.Abs(axialOrigin) > 1)
                    return false;
            }
            else
            {
                var near = (-1 - axialOrigin) / axialRay;
                var far = (1 - axialOrigin) / axialRay;
                enter = Math.Max(enter, Math.Min(near, far));
                exit = Math.Min(exit, Math.Max(near, far));
            }
            if (exit < Math.Max(enter, 0.0001f))
                return false;
            distance = enter >= 0.0001f ? enter : exit;
            return float.IsFinite(distance);
        }
        if (box.Shape is CMU3DPartShape.WedgeY or CMU3DPartShape.WedgeYReverse)
        {
            var slope = box.Shape == CMU3DPartShape.WedgeYReverse ? 1 : -1;
            var planeOrigin = localOrigin.Z / box.HalfSize.Z + slope * localOrigin.Y / box.HalfSize.Y;
            var planeRay = localRay.Z / box.HalfSize.Z + slope * localRay.Y / box.HalfSize.Y;
            if (MathF.Abs(planeRay) < 0.000001f)
            {
                if (planeOrigin > 0)
                    return false;
            }
            else if (planeRay < 0)
                enter = MathF.Max(enter, -planeOrigin / planeRay);
            else
                exit = MathF.Min(exit, -planeOrigin / planeRay);
        }
        for (var axis = 0; axis < 3; axis++)
        {
            var o = CMU3DModelFace.Coordinate(localOrigin, axis);
            var d = CMU3DModelFace.Coordinate(localRay, axis);
            var h = CMU3DModelFace.Coordinate(box.HalfSize, axis);
            if (MathF.Abs(d) < 0.000001f)
            {
                if (o < -h || o > h)
                    return false;
                continue;
            }
            var a = (-h - o) / d;
            var b = (h - o) / d;
            enter = Math.Max(enter, Math.Min(a, b));
            exit = Math.Min(exit, Math.Max(a, b));
        }
        if (exit < Math.Max(enter, 0.0001f))
            return false;
        distance = enter >= 0.0001f ? enter : exit;
        if (box.SurfaceIndex != 0)
        {
            if (surfaces == null)
                return false;
            var sample = (localOrigin + localRay * distance) / (box.HalfSize * 2);
            if (!surfaces.OpaqueAt(box, sample))
            {
                // A cutout in the entry face may expose an opaque part of the exit face.
                distance = exit;
                sample = (localOrigin + localRay * distance) / (box.HalfSize * 2);
                if (!surfaces.OpaqueAt(box, sample))
                    return false;
            }
        }
        return float.IsFinite(distance);
    }

    private void WriteBox(int index, CMU3DSceneBox box) => WriteBox(BoxPixels, index, box);

    public static void WriteBox(Span<Rgba32> pixels, int index, CMU3DSceneBox box)
    {
        var offset = index * BoxTexels;
        pixels[offset] = Pair(Signed(box.Center.X, CoordinateScale), Signed(box.Center.Y, CoordinateScale));
        pixels[offset + 1] = Pair(Signed(box.Center.Z, CoordinateScale), Unsigned(box.HalfSize.X, HalfSizeScale));
        pixels[offset + 2] = Pair(Unsigned(box.HalfSize.Y, HalfSizeScale), Unsigned(box.HalfSize.Z, HalfSizeScale));
        pixels[offset + 3] = Pair(Signed(MathF.Cos(box.Yaw), RotationScale), Signed(MathF.Sin(box.Yaw), RotationScale));
        pixels[offset + 4] = new Rgba32(Channel(box.Color.R), Channel(box.Color.G), Channel(box.Color.B), Channel(box.Color.A));
        // G bit 7 distinguishes untextured tilt from the existing atlas metadata.
        if (box.Pitch != 0)
        {
            var pitch = EncodePitch(box.Pitch);
            pixels[offset + 5] = new Rgba32((byte) box.Shape, (byte) (128 + (pitch >> 8)), 0, (byte) (pitch & 255));
            return;
        }
        // G: projection (bits 0..1), U flip (bit 2), surface high nibble (bits 3..6).
        // B: surface low byte. Existing slots retain their original packed representation.
        pixels[offset + 5] = new Rgba32((byte) box.Shape,
            box.SurfaceIndex == 0 ? (byte) 0 : (byte) ((byte) box.SurfaceAxis + (box.SurfaceFlipU ? 4 : 0) + (box.SurfaceIndex >> 8) * 8),
            (byte) (box.SurfaceIndex & 255), box.SurfaceIndex == 0 ? (byte) 0 : Channel(box.SurfaceVScale));
    }

    public static CMU3DSceneBox Quantize(CMU3DSceneBox box)
    {
        var center = new Vector3(DecodeSigned(Signed(box.Center.X, CoordinateScale), CoordinateScale),
            DecodeSigned(Signed(box.Center.Y, CoordinateScale), CoordinateScale), DecodeSigned(Signed(box.Center.Z, CoordinateScale), CoordinateScale));
        var half = new Vector3(Unsigned(box.HalfSize.X, HalfSizeScale) / HalfSizeScale,
            Unsigned(box.HalfSize.Y, HalfSizeScale) / HalfSizeScale, Unsigned(box.HalfSize.Z, HalfSizeScale) / HalfSizeScale);
        var c = DecodeSigned(Signed(MathF.Cos(box.Yaw), RotationScale), RotationScale);
        var s = DecodeSigned(Signed(MathF.Sin(box.Yaw), RotationScale), RotationScale);
        return box with { Center = center, HalfSize = half, Yaw = MathF.Atan2(s, c),
            Pitch = DecodePitch(EncodePitch(box.Pitch)), SurfaceVScale = Channel(box.SurfaceVScale) / 255f };
    }

    private static bool Valid(CMU3DSceneBox box) => (byte) box.Shape <= (byte) CMU3DPartShape.Foliage &&
        float.IsFinite(box.Pitch) && MathF.Abs(box.Pitch) <= MathF.PI / 2 && (box.Pitch == 0 || box.SurfaceIndex == 0) &&
        box.SurfaceIndex <= CMU3DSurfacePrototype.MaximumAtlasIndex &&
        (box.SurfaceIndex == 0 || box.Shape is CMU3DPartShape.Box or CMU3DPartShape.WedgeY or CMU3DPartShape.WedgeYReverse && box.SurfaceAxis is CMU3DSurfaceAxis.XZ or CMU3DSurfaceAxis.XY or CMU3DSurfaceAxis.YZ) &&
        float.IsFinite(box.SurfaceVScale) && box.SurfaceVScale is >= 0 and <= 1 && Finite(box.Center) && Finite(box.HalfSize) &&
        MathF.Abs(box.Center.X) <= 31 && MathF.Abs(box.Center.Y) <= 31 && MathF.Abs(box.Center.Z) <= 31 &&
        box.HalfSize.X is > 0 and <= 31 && box.HalfSize.Y is > 0 and <= 31 && box.HalfSize.Z is > 0 and <= 31 &&
        float.IsFinite(box.Yaw) && float.IsFinite(box.Color.R) && float.IsFinite(box.Color.G) && float.IsFinite(box.Color.B) && float.IsFinite(box.Color.A);
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static ushort EncodePitch(float value) => (ushort) Math.Clamp((int) MathF.Round(value * (32768 / MathF.PI) + 16384), 0, 32767);
    private static float DecodePitch(ushort value) => (value - 16384) * (MathF.PI / 32768);
    private static ushort Signed(float value, float scale) => (ushort) Math.Clamp((int) MathF.Round(value * scale + 32768), 0, 65535);
    private static ushort Unsigned(float value, float scale) => (ushort) Math.Clamp((int) MathF.Round(value * scale), 0, 65535);
    private static float DecodeSigned(ushort value, float scale) => (value - 32768) / scale;
    private static byte Channel(float value) => (byte) Math.Clamp((int) MathF.Round(value * 255), 0, 255);
    private static Rgba32 Pair(ushort a, ushort b) => new((byte) a, (byte) (a >> 8), (byte) b, (byte) (b >> 8));
}

public readonly record struct CMU3DSceneHit(Vector3 Position, float Distance, EntityUid? Source, int BoxIndex);
