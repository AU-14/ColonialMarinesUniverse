namespace Content.Server._CMU14.Round.Objectives.Component;

[RegisterComponent]
public sealed partial class ObjSpawnedByComponent : Robust.Shared.GameObjects.Component
{
    // Ojective entity that spawned this entity, used by cleanup
    public EntityUid ObjectiveUid;
}
