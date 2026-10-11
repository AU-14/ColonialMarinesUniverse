using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneCatalog
{
    /// <summary>Only audited exact disposal sources may open a slab or select their loose pose.</summary>
    public CMU3DSceneMatch? WithAnchorState(CMU3DSceneMatch? match, bool anchored)
    {
        if (match is not { Exact: true } value || !ValidAnchorPose(value.Model, value.Reference) ||
            value.Model.AlternateAnchorModel is not { } alternate ||
            !_models.TryGetValue(alternate.Id, out var other) ||
            !ValidAnchorPose(other, value.Reference) || other.Anchored == value.Model.Anchored ||
            other.AlternateAnchorModel?.Id != value.Model.ID)
            return null;
        return value.Model.Anchored == anchored ? value : value with { Model = other };
    }

    private static bool ValidAnchorPose(CMU3DModelPrototype model, string reference)
    {
        var suffix = reference switch
        {
            "DisposalJunction" => "j1",
            "DisposalJunctionFlipped" => "j2",
            "DisposalXJunction" => "x",
            "DisposalPipe" => "s",
            "DisposalBend" => "c",
            "DisposalTrunk" => "t",
            "DisposalRouter" => "j1s",
            "DisposalRouterFlipped" => "j2s",
            "DisposalYJunction" => "y",
            _ => null,
        };
        if (suffix == null || model.Anchored is not { } anchored || model.ReferencePrototype != reference ||
            model.ReferenceRsi != "Structures/Piping/disposal.rsi" || model.SourceDirections != 1 ||
            model.ReferenceDirection is not (null or 0) || model.SourceSpriteOffset != System.Numerics.Vector2.Zero ||
            model.GroundOffset != System.Numerics.Vector2.Zero || model.Placement != "floor" ||
            !model.UseEntityRotation || model.SourceSpriteRotates == anchored ||
            (model.FloorOpening != null) != anchored || model.CeilingOpening != null ||
            model.PreserveSlabCladding != anchored || model.AlternateFoldModel != null || model.AlternateDoorModel != null ||
            model.DirectionalModels.Length != 0 || model.RandomSpritePrototypes.Length != 0 ||
            model.FloorOpeningCompanions.Count != 0 || model.AlternateAnchorModel?.Id == model.ID)
            return false;
        if (anchored ? model.SourcePrototypes.Length != 1 || model.SourcePrototypes[0] != reference : model.SourcePrototypes.Length != 0)
            return false;
        var state = (anchored ? "pipe-" : "conpipe-") + suffix;
        return model.ReferenceState == state && model.SpriteStates.Count == 1 &&
               model.SpriteStates.TryGetValue(state, out var pose) && pose.Frames.Count == 1 &&
               pose.Delays.Count == 1 && pose.Delays[0] == 1f;
    }
}
