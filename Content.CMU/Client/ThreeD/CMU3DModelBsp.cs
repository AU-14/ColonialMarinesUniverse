using System.Numerics;

namespace Content.Client.CMU14.ThreeD;

/// <summary>
/// Cached binary space partition of axis-aligned rectangular model faces. Splitting rectangles on
/// axis planes gives a valid back-to-front order even when face centers have the opposite order.
/// </summary>
public sealed class CMU3DModelBsp
{
    public const int MaxInputFaces = 128 * 6;
    public const int MaxFragments = 16384;
    public const int MaxDepth = 64;
    private const int MaxPlaneTests = 2000000;
    private const int CandidateSamplesPerAxis = 9;

    private Node? _root;
    private int _planeTests;
    public int FragmentCount { get; private set; }
    public int Depth { get; private set; }

    /// <summary>Reject over-budget input instead of silently reverting to incorrect face sorting.</summary>
    public bool TryBuild(IReadOnlyList<CMU3DModelFace> faces)
    {
        _root = null;
        _planeTests = 0;
        FragmentCount = faces.Count;
        Depth = 0;
        if (faces.Count > MaxInputFaces)
            return false;
        var source = new List<CMU3DModelFace>(faces.Count);
        for (var i = 0; i < faces.Count; i++)
            source.Add(faces[i]);
        if (!TryBuildNode(source, 1, out var root))
            return false;
        _root = root;
        return true;
    }

    public void CollectVisibleFaces(Vector3 eye, List<CMU3DModelFace> output)
    {
        output.Clear();
        Visit(_root, eye, output);
    }

    private static void Visit(Node? node, Vector3 eye, List<CMU3DModelFace> output)
    {
        if (node == null)
            return;
        var positive = CMU3DModelFace.Coordinate(eye, node.Plane.Axis) >= node.Plane.Position;
        Visit(positive ? node.Negative : node.Positive, eye, output);
        foreach (var face in node.Faces)
        {
            if ((CMU3DModelFace.Coordinate(eye, face.Axis) - face.Position) * face.Facing > 0)
                output.Add(face);
        }
        Visit(positive ? node.Positive : node.Negative, eye, output);
    }

    private bool TryBuildNode(List<CMU3DModelFace> faces, int depth, out Node? node)
    {
        node = null;
        if (faces.Count == 0)
            return true;
        if (depth > MaxDepth || !TryChoosePlane(faces, out var plane))
            return false;
        Depth = Math.Max(Depth, depth);
        var negative = new List<CMU3DModelFace>();
        var positive = new List<CMU3DModelFace>();
        var coplanar = new List<CMU3DModelFace>();
        foreach (var face in faces)
        {
            var low = CMU3DModelFace.Coordinate(face.Min, plane.Axis);
            var high = CMU3DModelFace.Coordinate(face.Max, plane.Axis);
            if (face.Axis == plane.Axis && face.Position == plane.Position)
                coplanar.Add(face);
            else if (high <= plane.Position)
                negative.Add(face);
            else if (low >= plane.Position)
                positive.Add(face);
            else
            {
                if (++FragmentCount > MaxFragments)
                    return false;
                // Both intervals have positive width. The face remains a rectangle on its original plane.
                negative.Add(face with { Max = CMU3DModelFace.WithCoordinate(face.Max, plane.Axis, plane.Position) });
                positive.Add(face with { Min = CMU3DModelFace.WithCoordinate(face.Min, plane.Axis, plane.Position) });
            }
        }
        // Exact coplanar overlap uses authored source order: the later part wins consistently.
        coplanar.Sort(static (a, b) => a.SourceOrder.CompareTo(b.SourceOrder));
        if (!TryBuildNode(negative, depth + 1, out var negativeNode) ||
            !TryBuildNode(positive, depth + 1, out var positiveNode))
            return false;
        node = new Node(plane, coplanar, negativeNode, positiveNode);
        return true;
    }

    private bool TryChoosePlane(List<CMU3DModelFace> faces, out Plane chosen)
    {
        chosen = default;
        var planes = new HashSet<Plane>();
        foreach (var face in faces)
            planes.Add(new Plane(face.Axis, face.Position));
        var candidates = new List<Plane>(planes);
        candidates.Sort(static (a, b) => a.Axis != b.Axis ? a.Axis.CompareTo(b.Axis) : a.Position.CompareTo(b.Position));
        var bestScore = int.MaxValue;
        // Sample quantiles per axis rather than testing every face against every possible plane.
        // A fixed classification budget and depth cap keep malicious or accidental input bounded.
        for (var start = 0; start < candidates.Count;)
        {
            var end = start + 1;
            while (end < candidates.Count && candidates[end].Axis == candidates[start].Axis)
                end++;
            var count = end - start;
            var samples = Math.Min(CandidateSamplesPerAxis, count);
            for (var sample = 0; sample < samples; sample++)
            {
                var candidate = candidates[start + (samples == 1 ? 0 : sample * (count - 1) / (samples - 1))];
                var negative = 0;
                var positive = 0;
                var split = 0;
                var coplanar = 0;
                foreach (var face in faces)
                {
                    if (++_planeTests > MaxPlaneTests)
                        return false;
                    var low = CMU3DModelFace.Coordinate(face.Min, candidate.Axis);
                    var high = CMU3DModelFace.Coordinate(face.Max, candidate.Axis);
                    if (face.Axis == candidate.Axis && face.Position == candidate.Position)
                        coplanar++;
                    else if (high <= candidate.Position)
                        negative++;
                    else if (low >= candidate.Position)
                        positive++;
                    else
                    {
                        negative++;
                        positive++;
                        split++;
                    }
                }
                var score = Math.Abs(positive - negative) * 2 + split * 5 - coplanar;
                if (score >= bestScore)
                    continue;
                bestScore = score;
                chosen = candidate;
            }
            start = end;
        }
        return bestScore != int.MaxValue;
    }

    private readonly record struct Plane(int Axis, float Position);
    private sealed record Node(Plane Plane, List<CMU3DModelFace> Faces, Node? Negative, Node? Positive);
}

/// <summary>A rectangle with equal Min/Max on Axis (0=X, 1=Y, 2=Z), and Facing -1 or +1.</summary>
public readonly record struct CMU3DModelFace(Vector3 Min, Vector3 Max, int Axis, int Facing, Color Color, int SourceOrder)
{
    public float Position => Coordinate(Min, Axis);
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Normal => WithCoordinate(Vector3.Zero, Axis, Facing);

    public void Corners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
    {
        a = Min;
        c = Max;
        switch (Axis)
        {
            case 0:
                b = new Vector3(Min.X, Max.Y, Min.Z);
                d = new Vector3(Min.X, Min.Y, Max.Z);
                break;
            case 1:
                b = new Vector3(Min.X, Min.Y, Max.Z);
                d = new Vector3(Max.X, Min.Y, Min.Z);
                break;
            default:
                b = new Vector3(Max.X, Min.Y, Min.Z);
                d = new Vector3(Min.X, Max.Y, Min.Z);
                break;
        }
        if (Facing < 0)
            (b, d) = (d, b);
    }

    public static float Coordinate(Vector3 vector, int axis) => axis switch
    {
        0 => vector.X,
        1 => vector.Y,
        _ => vector.Z,
    };

    public static Vector3 WithCoordinate(Vector3 vector, int axis, float coordinate) => axis switch
    {
        0 => new Vector3(coordinate, vector.Y, vector.Z),
        1 => new Vector3(vector.X, coordinate, vector.Z),
        _ => new Vector3(vector.X, vector.Y, coordinate),
    };
}
