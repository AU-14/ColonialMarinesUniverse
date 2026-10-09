using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Clips plain thin cladding after the caller proves the exact source, anchoring and shared tile.</summary>
public static class CMU3DSlabCladding
{
    public static bool TryClip(CMU3DSceneBox box, Vector2 tileCenterRelativeOrigin, float gridYaw,
        IReadOnlyList<Box2> tileCenteredOpenings, out List<CMU3DSceneBox> fragments)
    {
        // Collection expressions emit CollectionsMarshal.SetCount, which the content sandbox forbids.
        fragments = new List<CMU3DSceneBox> { box };
        if (box.Shape != CMU3DPartShape.Box || box.SurfaceIndex != 0 || box.Pitch != 0 ||
            !Finite(box.Center) || !Finite(box.HalfSize) || box.HalfSize.X <= 0 || box.HalfSize.Y <= 0 || box.HalfSize.Z <= 0 ||
            !float.IsFinite(tileCenterRelativeOrigin.X) || !float.IsFinite(tileCenterRelativeOrigin.Y) ||
            !float.IsFinite(box.Yaw) || !float.IsFinite(gridYaw) ||
            box.Center.Z - box.HalfSize.Z < -.1f || box.Center.Z + box.HalfSize.Z > .04f)
            return false;

        var relative = Math.IEEERemainder((double) box.Yaw - gridYaw, Math.Tau);
        var turn = (int) Math.Round(relative / (Math.PI / 2));
        if (Math.Abs(relative - turn * (Math.PI / 2)) > CMU3DSlabOpening.CardinalTolerance)
            return false;
        var c = MathF.Cos(gridYaw);
        var s = MathF.Sin(gridYaw);
        var delta = new Vector2(box.Center.X, box.Center.Y) - tileCenterRelativeOrigin;
        var center = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        var half = (turn & 1) == 0
            ? new Vector2(box.HalfSize.X, box.HalfSize.Y)
            : new Vector2(box.HalfSize.Y, box.HalfSize.X);
        var slab = new Box2(center - half, center + half);
        if (!CMU3DSlabOpening.TrySubtract(slab, tileCenteredOpenings, out var rectangles))
            return false;
        if (rectangles.Count == 1 && rectangles[0] == slab)
            return true;

        var result = new List<CMU3DSceneBox>(rectangles.Count);
        foreach (var rectangle in rectangles)
        {
            var middle = (rectangle.BottomLeft + rectangle.TopRight) / 2;
            var world = tileCenterRelativeOrigin + new Vector2(c * middle.X - s * middle.Y, s * middle.X + c * middle.Y);
            if (!float.IsFinite(world.X) || !float.IsFinite(world.Y))
                return false;
            result.Add(box with
            {
                Center = new Vector3(world, box.Center.Z),
                HalfSize = new Vector3(rectangle.Width / 2, rectangle.Height / 2, box.HalfSize.Z),
                Yaw = gridYaw,
            });
        }
        fragments = result;
        return true;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
