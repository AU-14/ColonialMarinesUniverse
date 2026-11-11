using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Explicit contact point for static loose props whose saved pivot overhangs a support.</summary>
public static class CMU3DSupportProbe
{
    public static bool TryPoint(CMU3DModelPrototype model, out Vector2 point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(model.SupportProbePart) || model.Placement != "surface" ||
            model.SourceDirections != 1 || !model.SourceSpriteRotates || !model.UseEntityRotation ||
            model.ConnectToNeighbours || model.FrameAnimations.Count != 0 || model.SpriteStates.Count != 1 ||
            model.ReferenceState == null || !model.SpriteStates.TryGetValue(model.ReferenceState, out var state) ||
            state.Frames.Count != 1 || state.Delays.Count != 1 || !float.IsFinite(state.Delays[0]) || state.Delays[0] <= 0 ||
            state.Frames[0].Parts.Count != model.Parts.Count || model.Parts.Count == 0)
            return false;

        CMU3DModelPart? selected = null;
        var bottom = float.PositiveInfinity;
        for (var i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            var frame = state.Frames[0].Parts[i];
            if (!part.Valid || !WithinBudget(part.Min) || !WithinBudget(part.Max) || !SamePart(part, frame))
                return false;
            part.Bounds(out var min, out _);
            bottom = MathF.Min(bottom, min.Z);
            if (part.Label != model.SupportProbePart)
                continue;
            if (selected != null || part.Shape != CMU3DPartShape.Box || part.Yaw != 0 || part.Pitch != 0 ||
                part.Surface != null || part.Color.A != 1 || part.OmitWhenConnected != 0)
                return false;
            selected = part;
        }
        if (selected == null || MathF.Abs(selected.Min.Z - bottom) > .000001f)
            return false;
        point = new Vector2((selected.Min.X + selected.Max.X) / 2, (selected.Min.Y + selected.Max.Y) / 2);
        return true;
    }

    private static bool SamePart(CMU3DModelPart a, CMU3DModelPart b) =>
        a.Label == b.Label && a.Shape == b.Shape && a.Yaw == b.Yaw && a.Pitch == b.Pitch &&
        a.Min == b.Min && a.Max == b.Max && a.Color == b.Color && a.Surface == b.Surface &&
        a.SurfaceAxis == b.SurfaceAxis && a.SurfaceFlipU == b.SurfaceFlipU && a.OmitWhenConnected == b.OmitWhenConnected;

    private static bool WithinBudget(Vector3 value) =>
        MathF.Abs(value.X) <= 32 && MathF.Abs(value.Y) <= 32 && MathF.Abs(value.Z) <= 32;
}
