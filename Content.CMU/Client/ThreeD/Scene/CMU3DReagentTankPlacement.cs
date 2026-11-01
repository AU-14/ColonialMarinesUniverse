using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Three exact thin grates may raise wheel bottoms; unknown or elevated supports are not guessed.</summary>
public static class CMU3DReagentTankPlacement
{
    public static float Offset(CMU3DModelPrototype model, CMU3DModelPrototype support, string prototype, Vector2 delta, float yaw)
    {
        if (model.ReagentTankAppearance == null || prototype is not ("CMCatwalk" or "CMCatwalkPrison" or "RMCCatwalkHybrisaElevator") ||
            Array.IndexOf(support.SourcePrototypes, prototype) < 0 || model.Placement != "floor" || support.Placement != "floor" ||
            !float.IsFinite(delta.X) || !float.IsFinite(delta.Y) || MathF.Abs(delta.X) > .0001f || MathF.Abs(delta.Y) > .0001f ||
            !float.IsFinite(yaw) || MathF.Abs(MathF.Sin(yaw * 2)) > .0001f || support.GroundOffset != Vector2.Zero ||
            support.WallMounted || support.ConnectToNeighbours || support.BackWallMountTargets.Length > 0 ||
            support.WindowMountTargets.Length > 0 || support.SpriteStates.Count > 0 || support.ReagentTankAppearance != null ||
            support.TerrainCutoutTargets.Length > 0 || support.Parts.Count == 0 || model.Parts.Count == 0)
            return 0;
        var top = 0f;
        foreach (var part in support.Parts)
        {
            if (!part.Valid || part.Shape != CMU3DPartShape.Box || part.Yaw != 0 || part.Pitch != 0 ||
                part.Min.X < -.50001f || part.Min.Y < -.50001f || part.Max.X > .50001f || part.Max.Y > .50001f ||
                part.Min.Z < 0 || part.Max.Z > .05f)
                return 0;
            top = Math.Max(top, part.Max.Z);
        }
        var bottom = float.PositiveInfinity;
        foreach (var part in model.Parts)
        {
            if (!part.Valid)
                return 0;
            part.Bounds(out var min, out var max);
            if (min.X < -.50001f || min.Y < -.50001f || max.X > .50001f || max.Y > .50001f)
                return 0;
            bottom = Math.Min(bottom, min.Z);
        }
        return MathF.Abs(bottom) <= .00001f ? top + .002f : 0;
    }
}
