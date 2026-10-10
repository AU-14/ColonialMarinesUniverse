using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Distance detail for solid tile walls; openings and non-solid assemblies retain their authored parts.</summary>
public static class CMU3DSceneDetail
{
    public static IReadOnlyList<CMU3DModelPart> DistantWall(IReadOnlyList<CMU3DModelPart> parts)
    {
        CMU3DModelPart? core = null;
        var top = float.NegativeInfinity;
        foreach (var part in parts)
        {
            if (!part.Valid) return parts;
            part.Bounds(out var min, out var max);
            // Do not simplify oversized, tilted or recessed assemblies into a tile wall.
            if (min.X < -.6f || min.Y < -.6f || max.X > .6f || max.Y > .6f || min.Z < -.1f || max.Z > 3.25f)
                return parts;
            top = Math.Max(top, max.Z);
            if (part.Shape == CMU3DPartShape.Box && part.Yaw == 0 && part.Pitch == 0 &&
                part.Surface == null && part.Color.A == 1 && min.Z <= .05f && max.Z >= 1.5f &&
                min.X <= -.49f && min.Y <= -.49f && max.X >= .49f && max.Y >= .49f)
                core = part;
        }
        if (core == null || parts.Count < 2) return parts;
        // Keep the solid footprint and full height. Small face relief and an uneven
        // crown become one distant shell; nearby geometry remains the original model.
        return [new CMU3DModelPart
        {
            Min = core.Min,
            Max = new Vector3(core.Max.X, core.Max.Y, top),
            Color = core.Color,
        }];
    }
}
