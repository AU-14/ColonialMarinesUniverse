using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>A mounted paper's rectangle and depth interval, before stacking. Depth increases toward its front.</summary>
public readonly record struct CMU3DWallPaperPose(EntityUid Uid, Vector2 SourcePosition, int DrawDepth, uint RenderOrder,
    float FrontYaw, Box2 Face, float Rear, float Front);

/// <summary>Bounded presentation-only paper layering. The canonical source view is unrotated; camera motion never reorders paper.</summary>
public static class CMU3DWallPaper
{
    public const int Limit = 16;
    public const float Gap = .004f;
    public const float MaxOffset = .25f;

    public static bool ValidModel(CMU3DModelPrototype model)
    {
        if (!model.WallPaper || !model.WallMounted || !model.FitInsideWall || model.BackWallMountTargets.Length == 0 ||
            model.SourceDirections != 1 || model.GroundOffset != Vector2.Zero || model.BakedSpriteTint != Color.White ||
            model.Placement != "floor" || model.ConnectToNeighbours || model.FaceAwayFromWall ||
            model.DirectionalModels.Length > 0 || model.SpriteStates.Count > 0 || model.WindowMountTargets.Length > 0 ||
            model.DoorSpriteStates.Count > 0 || model.DoorButtonStates.Count > 0 || model.PoweredLightStates.Count > 0 ||
            model.BarricadeDamageStates.Count > 0 || model.ReagentTankAppearance != null || model.TerrainCutoutTargets.Length > 0 ||
            model.FloorOpening != null || model.CeilingOpening != null || model.AlternateDoorModel != null ||
            model.AlternateFoldModel != null || model.PanelEndTargets.Length > 0 ||
            model.Parts.Count is < 1 or > 16 || string.IsNullOrEmpty(model.ReferenceRsi) || string.IsNullOrEmpty(model.ReferenceState))
            return false;
        foreach (var part in model.Parts)
            if (!part.Valid || part.Shape != CMU3DPartShape.Box || part.Yaw != 0 || part.Pitch != 0 || part.OmitWhenConnected != 0)
                return false;
        var min = model.Parts.Select(p => p.Min).Aggregate(Vector3.Min);
        var max = model.Parts.Select(p => p.Max).Aggregate(Vector3.Max);
        return max.X - min.X <= 1 && max.Z - min.Z <= 1 && max.Y - min.Y <= .02f &&
               min.X >= -.5f && max.X <= .5f && min.Y >= -.55f && max.Y <= -.45f;
    }

    public static bool TryPose(CMU3DModelPrototype model, EntityUid uid, Vector2 sourcePosition, int drawDepth, uint renderOrder,
        Vector2 mountedPosition, float yaw, bool inside, out CMU3DWallPaperPose pose)
    {
        pose = default;
        if (!ValidModel(model) || !float.IsFinite(yaw) || !float.IsFinite(mountedPosition.X) || !float.IsFinite(mountedPosition.Y))
            return false;
        var parts = inside ? CMU3DSceneLayout.InsideWallParts(model.Parts) : model.Parts.ToArray();
        var min = parts.Select(p => p.Min).Aggregate(Vector3.Min);
        var max = parts.Select(p => p.Max).Aggregate(Vector3.Max);
        var frontYaw = yaw + (inside ? MathF.PI : 0);
        var tangent = new Vector2(MathF.Cos(frontYaw), MathF.Sin(frontYaw));
        var normal = new Vector2(tangent.Y, -tangent.X);
        var x = Vector2.Dot(mountedPosition, tangent);
        var depth = Vector2.Dot(mountedPosition, normal);
        pose = new(uid, sourcePosition, drawDepth, renderOrder, frontYaw,
            inside ? new Box2(x - max.X, min.Z, x - min.X, max.Z) : new Box2(x + min.X, min.Z, x + max.X, max.Z),
            depth + (inside ? min.Y : -max.Y), depth + (inside ? max.Y : -min.Y));
        return float.IsFinite(sourcePosition.X) && float.IsFinite(sourcePosition.Y);
    }

    public static bool Overlaps(CMU3DWallPaperPose a, CMU3DWallPaperPose b) =>
        MathF.Abs(MathF.Sin((a.FrontYaw - b.FrontYaw) / 2)) < .00001f &&
        MathF.Abs(a.Rear - b.Rear) < .025f &&
        MathF.Min(a.Face.Right, b.Face.Right) - MathF.Max(a.Face.Left, b.Face.Left) > .00001f &&
        MathF.Min(a.Face.Top, b.Face.Top) - MathF.Max(a.Face.Bottom, b.Face.Bottom) > .00001f;

    public static bool TryOffsets(IReadOnlyList<CMU3DWallPaperPose> papers, out Dictionary<EntityUid, Vector2> offsets)
    {
        offsets = [];
        if (papers.Count is < 1 or > Limit || papers.Select(p => p.Uid).Distinct().Count() != papers.Count)
            return false;
        foreach (var p in papers)
            if (!float.IsFinite(p.FrontYaw) || !float.IsFinite(p.SourcePosition.Y) || !float.IsFinite(p.Rear) || !float.IsFinite(p.Front) ||
                p.Front <= p.Rear || p.Front - p.Rear > .02001f || !float.IsFinite(p.Face.Left) || !float.IsFinite(p.Face.Right) ||
                !float.IsFinite(p.Face.Top) || !float.IsFinite(p.Face.Bottom) || p.Face.Width <= 0 || p.Face.Height <= 0)
                return false;
        // For equal 32x32 identity source canvases, Clyde's screen-BB Top order is descending world Y.
        var ordered = papers.OrderBy(p => p.DrawDepth).ThenBy(p => p.RenderOrder)
            .ThenByDescending(p => p.SourcePosition.Y).ThenBy(p => p.Uid).ToArray();
        var distances = new float[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            var paper = ordered[i];
            for (var j = 0; j < i; j++)
                if (Overlaps(paper, ordered[j]))
                    distances[i] = MathF.Max(distances[i], ordered[j].Front + distances[j] + Gap - paper.Rear);
            if (!float.IsFinite(distances[i]) || distances[i] > MaxOffset)
                return false;
        }
        for (var i = 0; i < ordered.Length; i++)
            offsets[ordered[i].Uid] = distances[i] * new Vector2(MathF.Sin(ordered[i].FrontYaw), -MathF.Cos(ordered[i].FrontYaw));
        return true;
    }
}
