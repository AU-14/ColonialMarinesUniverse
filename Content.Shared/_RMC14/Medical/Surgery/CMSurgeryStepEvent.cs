namespace Content.Shared._RMC14.Medical.Surgery;

/// <summary>
///     Raised on the step entity.
/// </summary>
[ByRefEvent]
public record struct CMSurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools)
{
    // CMU executes anatomical effects before committing the framework's markers.
    public bool DeferMarkers;
    public bool Failed;
    public bool ToolCheckPassed;
    public EntityUid? Used;
    // Local operation ownership guard; never part of a network message.
    public Func<bool>? IsCurrent;
    public Content.Shared.Body.Part.BodyPartType? TargetType;
    public Content.Shared.Body.Part.BodyPartSymmetry? TargetSymmetry;
}
