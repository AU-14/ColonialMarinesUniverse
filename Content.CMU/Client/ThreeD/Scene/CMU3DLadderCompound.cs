using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Conservative source and admission gates for the one authored ladder/pallet/carton composition.</summary>
public static class CMU3DLadderCompound
{
    public const string OwnerPrototype = "CMUZLevelLadderThroughDown2";
    public const string OwnerModel = "CMU3DLadderThroughDown2";
    public const float Tolerance = .00001f;

    public static bool ValidContract(CMU3DModelPrototype model)
    {
        if (model.ID != OwnerModel || model.SourcePrototypes.Length != 1 ||
            model.SourcePrototypes[0] != OwnerPrototype || model.FloorOpening == null ||
            model.CeilingOpening != null || model.FloorOpeningCompanions.Count != 2)
            return false;
        var pallet = false;
        var cardboard = false;
        foreach (var companion in model.FloorOpeningCompanions)
        {
            if (companion.Prototype == "DecorFloorPallet" && companion.Model == "CMU3DTimberPallet" && !pallet)
                pallet = true;
            else if (companion.Prototype == "DecorFloorCardboard" && companion.Model == "CMU3DDecorFloorCardboardEast" && !cardboard)
                cardboard = true;
            else
                return false;
            if (string.IsNullOrEmpty(companion.ReferenceRsi) || string.IsNullOrEmpty(companion.ReferenceState) ||
                companion.SourceDirections is not (4 or 8) || companion.ReferenceDirection < 0 ||
                companion.ReferenceDirection >= companion.SourceDirections || companion.SourceFrame != 0 ||
                !companion.SourceNoRotation || companion.SourceSnapCardinals ||
                companion.SourceSpriteOffset != Vector2.Zero || !float.IsFinite(companion.SourceYaw) ||
                !float.IsFinite(companion.RenderYaw) || companion.RenderOffset.X != 0 || companion.RenderOffset.Y != 0 ||
                !float.IsFinite(companion.RenderOffset.Z) || companion.RenderOffset.Z is < 0 or > .5f ||
                companion.Parts.Count is < 1 or > 128)
                return false;
            foreach (var part in companion.Parts)
            {
                if (!part.Valid)
                    return false;
            }
        }
        return pallet && cardboard;
    }

    /// <summary>Count identities before eligibility checks, so an unsupported duplicate cannot be silently ignored.</summary>
    public static bool TrySelect(CMU3DModelPrototype owner, EntityUid grid, Vector2 pivot, float yaw,
        IReadOnlyList<CMU3DLadderCompanionSource> sources, out List<CMU3DLadderCompanionMatch> matches)
    {
        matches = [];
        if (!ValidContract(owner) || !float.IsFinite(yaw) || !float.IsFinite(pivot.X) || !float.IsFinite(pivot.Y))
            return false;
        foreach (var companion in owner.FloorOpeningCompanions)
        {
            CMU3DLadderCompanionSource? selected = null;
            foreach (var source in sources)
            {
                if (source.Prototype != companion.Prototype)
                    continue;
                if (selected != null)
                    return false;
                selected = source;
            }
            if (selected is not { } target || !Matches(companion, target, grid, pivot, yaw))
                return false;
            matches.Add(new CMU3DLadderCompanionMatch(companion, target));
        }
        return true;
    }

    public static bool Matches(CMU3DFloorOpeningCompanion contract, CMU3DLadderCompanionSource source,
        EntityUid grid, Vector2 pivot, float ownerYaw)
    {
        if (!source.Eligible || source.Model is not { } model || model.ID != contract.Model || source.Grid != grid ||
            !float.IsFinite(source.Position.X) || !float.IsFinite(source.Position.Y) ||
            Vector2.DistanceSquared(source.Position, pivot) > Tolerance * Tolerance ||
            !SameAngle(source.SourceYaw - ownerYaw, contract.SourceYaw * MathF.PI / 180) ||
            !SameAngle(source.RenderYaw - ownerYaw, contract.RenderYaw * MathF.PI / 180) ||
            DirectionIndex(source.SourceYaw, contract.SourceDirections) != contract.ReferenceDirection ||
            source.NoRotation != contract.SourceNoRotation || source.SnapCardinals != contract.SourceSnapCardinals ||
            source.Offset != contract.SourceSpriteOffset || source.Scale != Vector2.One || source.Rotation != 0 ||
            source.Directions != contract.SourceDirections || source.FrameCount != 1 ||
            source.Layer.Frame != contract.SourceFrame || !source.Layer.Visible || !source.Layer.IdentityTransform ||
            source.Layer.Color != Color.White || NormalizeRsi(source.Layer.Rsi) != NormalizeRsi(contract.ReferenceRsi) ||
            source.Layer.State != contract.ReferenceState || model.SourceDirections != contract.SourceDirections ||
            NormalizeRsi(model.ReferenceRsi) != NormalizeRsi(contract.ReferenceRsi) || model.ReferenceState != contract.ReferenceState ||
            model.ReferenceDirection is { } direction && direction != contract.ReferenceDirection ||
            model.GroundOffset != Vector2.Zero || model.BakedSpriteTint != Color.White || model.ReferenceTint != Color.White ||
            model.SpriteStates.Count != 0 || model.FloorOpening != null || model.CeilingOpening != null ||
            model.FloorOpeningCompanions.Count != 0 || model.Parts.Count != contract.Parts.Count)
            return false;
        return true;
    }

    public static bool SameAngle(float left, float right) => float.IsFinite(left) && float.IsFinite(right) &&
        MathF.Abs(MathF.Sin((left - right) / 2)) <= Tolerance / 2;

    public static int DirectionIndex(float yaw, int directions)
    {
        if (!float.IsFinite(yaw) || directions is not (4 or 8))
            return -1;
        var turn = (int) MathF.Floor((yaw % MathF.Tau) / (MathF.Tau / directions) + .5f);
        turn = (turn % directions + directions) % directions;
        return directions == 4
            ? turn switch { 1 => 2, 2 => 1, 3 => 3, _ => 0 }
            : turn switch { 1 => 4, 2 => 2, 3 => 6, 4 => 1, 5 => 7, 6 => 3, 7 => 5, _ => 0 };
    }

    private static string? NormalizeRsi(string? value)
    {
        value = value?.TrimStart('/');
        return value?.StartsWith("Textures/", StringComparison.Ordinal) == true ? value[9..] : value;
    }

    /// <summary>Validate the actual quantized encoder outcome, including every anonymous slab fragment.</summary>
    public static bool AdmitsWholeTransaction(IReadOnlyList<CMU3DSceneBox> staged,
        IReadOnlyCollection<EntityUid> requiredSources, bool extended)
    {
        var encoder = new CMU3DSceneEncoding(extended);
        encoder.Build(staged);
        if (encoder.ReferenceBudgetExceeded)
            return false;
        var expected = new Dictionary<EntityUid, int>();
        foreach (var source in requiredSources)
            expected[source] = 0;
        var unowned = 0;
        foreach (var box in staged)
        {
            if (box.Source is not { } source)
                unowned++;
            else if (expected.TryGetValue(source, out var count))
                expected[source] = count + 1;
        }
        foreach (var box in encoder.Boxes)
        {
            if (box.Source is not { } source)
                unowned--;
            else if (expected.TryGetValue(source, out var count))
                expected[source] = count - 1;
        }
        if (unowned != 0)
            return false;
        foreach (var count in expected.Values)
        {
            if (count != 0)
                return false;
        }
        return true;
    }
}

public readonly record struct CMU3DLadderCompanionSource(EntityUid Uid, string Prototype,
    CMU3DModelPrototype? Model, EntityUid? Grid, Vector2 Position, float SourceYaw, float RenderYaw)
{
    public bool Eligible { get; init; }
    public CMU3DButtonLayer Layer { get; init; }
    public int Directions { get; init; }
    public int FrameCount { get; init; }
    public Vector2 Offset { get; init; }
    public Vector2 Scale { get; init; }
    public float Rotation { get; init; }
    public bool NoRotation { get; init; }
    public bool SnapCardinals { get; init; }
}

public readonly record struct CMU3DLadderCompanionMatch(CMU3DFloorOpeningCompanion Contract,
    CMU3DLadderCompanionSource Source);
