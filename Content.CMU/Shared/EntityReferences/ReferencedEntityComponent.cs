namespace Content.Shared.CMU14.EntityReferences;

/// <summary>Entities that must clear a reference before this entity is deleted.</summary>
[RegisterComponent]
public sealed partial class ReferencedEntityComponent : Component
{
    public readonly HashSet<EntityUid> Observers = new();
}

[ByRefEvent]
public readonly record struct ReferencedEntityTerminatingEvent(EntityUid Entity);
