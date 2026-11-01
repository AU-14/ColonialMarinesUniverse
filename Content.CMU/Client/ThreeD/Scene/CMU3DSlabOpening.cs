using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Bounded, presentation-only subtraction. Inputs and returned rectangles never change gameplay geometry.</summary>
public static class CMU3DSlabOpening
{
    public const int MaxOpenings = 16;
    public const int MaxFragments = 128;
    public const float CardinalTolerance = .0001f;

    public static bool ValidBounds(CMU3DSlabOpeningBounds? opening)
    {
        return opening != null && Finite(opening.Min) && Finite(opening.Max) &&
               opening.Min.X >= -.5f && opening.Min.Y >= -.5f &&
               opening.Max.X <= .5f && opening.Max.Y <= .5f &&
               opening.Min.X < opening.Max.X && opening.Min.Y < opening.Max.Y;
    }

    /// <summary>
    /// Projects an entity-local aperture into centered, unit-tile grid coordinates.
    /// The caller supplies the source's grid-local offset from the tile center, with no model offset added twice.
    /// Arbitrary grid yaw is supported; only the entity's yaw relative to that grid must be cardinal.
    /// An aperture that leaves this tile is unsupported rather than silently clipped or moved.
    /// </summary>
    public static bool TryTransform(CMU3DSlabOpeningBounds? opening, Vector2 sourceOffsetInTile,
        float entityYaw, float gridYaw, out Box2 projected)
    {
        projected = default;
        if (!ValidBounds(opening) || !Finite(sourceOffsetInTile) ||
            MathF.Abs(sourceOffsetInTile.X) > .5f || MathF.Abs(sourceOffsetInTile.Y) > .5f ||
            !float.IsFinite(entityYaw) || !float.IsFinite(gridYaw))
            return false;

        var relative = Math.IEEERemainder((double) entityYaw - gridYaw, Math.Tau);
        var quarter = (int) Math.Round(relative / (Math.PI / 2));
        if (Math.Abs(relative - quarter * (Math.PI / 2)) > CardinalTolerance)
            return false;

        var min = opening!.Min;
        var max = opening.Max;
        switch ((quarter % 4 + 4) % 4)
        {
            case 1:
                (min, max) = (new Vector2(-max.Y, min.X), new Vector2(-min.Y, max.X));
                break;
            case 2:
                (min, max) = (-max, -min);
                break;
            case 3:
                (min, max) = (new Vector2(min.Y, -max.X), new Vector2(max.Y, -min.X));
                break;
        }

        min += sourceOffsetInTile;
        max += sourceOffsetInTile;
        if (min.X < -.5f || min.Y < -.5f || max.X > .5f || max.Y > .5f)
            return false;
        var candidate = new Box2(min, max);
        if (!ValidRectangle(candidate))
            return false;
        projected = candidate;
        return true;
    }

    /// <summary>
    /// Subtracts a union of at most sixteen finite rectangles from a finite slab.
    /// Openings may extend outside the slab; their intersections are used. Exact duplicate intersections are ignored.
    /// Output is disjoint and preserves input edges. Failure returns the original valid slab atomically;
    /// an invalid slab returns no fragments. A smaller caller budget may be supplied, never a larger one.
    /// </summary>
    public static bool TrySubtract(Box2 slab, IReadOnlyList<Box2> openings, out List<Box2> fragments,
        int maxFragments = MaxFragments)
    {
        fragments = [];
        if (!ValidRectangle(slab))
            return false;
        fragments.Add(slab);
        if (openings == null || openings.Count > MaxOpenings || maxFragments < 1 || maxFragments > MaxFragments)
            return false;

        var cuts = new List<Box2>(openings.Count);
        foreach (var opening in openings)
        {
            if (!ValidRectangle(opening))
                return false;
            if (Intersection(slab, opening) is { } clipped && !cuts.Contains(clipped))
                cuts.Add(clipped);
        }

        var current = new List<Box2> { slab };
        foreach (var cut in cuts)
        {
            var next = new List<Box2>();
            foreach (var rectangle in current)
            {
                if (Intersection(rectangle, cut) is not { } intersection)
                    next.Add(rectangle);
                else
                {
                    // Full-height side strips plus the middle lower/upper strips have disjoint interiors.
                    Add(next, rectangle.Left, rectangle.Bottom, intersection.Left, rectangle.Top);
                    Add(next, intersection.Right, rectangle.Bottom, rectangle.Right, rectangle.Top);
                    Add(next, intersection.Left, rectangle.Bottom, intersection.Right, intersection.Bottom);
                    Add(next, intersection.Left, intersection.Top, intersection.Right, rectangle.Top);
                }

                if (next.Count > maxFragments)
                    return false;
            }
            current = next;
        }

        fragments = current;
        return true;
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool ValidRectangle(Box2 rectangle) =>
        float.IsFinite(rectangle.Left) && float.IsFinite(rectangle.Bottom) &&
        float.IsFinite(rectangle.Right) && float.IsFinite(rectangle.Top) &&
        float.IsFinite(rectangle.Width) && float.IsFinite(rectangle.Height) &&
        rectangle.Width > 0 && rectangle.Height > 0;

    private static Box2? Intersection(Box2 first, Box2 second)
    {
        var left = Math.Max(first.Left, second.Left);
        var bottom = Math.Max(first.Bottom, second.Bottom);
        var right = Math.Min(first.Right, second.Right);
        var top = Math.Min(first.Top, second.Top);
        return left < right && bottom < top ? new Box2(left, bottom, right, top) : null;
    }

    private static void Add(List<Box2> rectangles, float left, float bottom, float right, float top)
    {
        if (left < right && bottom < top)
            rectangles.Add(new Box2(left, bottom, right, top));
    }
}
