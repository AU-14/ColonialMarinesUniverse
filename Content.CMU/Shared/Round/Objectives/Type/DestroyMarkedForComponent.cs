namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class DestroyMarkedForComponent : Robust.Shared.GameObjects.Component
{
    public Dictionary<EntityUid, string> AssociatedObjectives = new();
}
