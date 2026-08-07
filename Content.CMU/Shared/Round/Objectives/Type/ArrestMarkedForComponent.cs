namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class ArrestMarkedForComponent : Robust.Shared.GameObjects.Component
{
    public Dictionary<EntityUid, string> AssociatedObjectives = new();
    public Dictionary<EntityUid, string?> AssociatedObjectiveJobs = new();
    public HashSet<EntityUid> CreditedObjectives = new();
}
