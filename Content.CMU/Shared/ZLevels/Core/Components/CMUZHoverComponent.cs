using Content.Shared.CMU14.ZLevels.Core.EntitySystems;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

/// <summary>
/// Server-side z-physics override that lifts an entity to <see cref="TargetMap"/>, holds it there,
/// and once <see cref="Descending"/> is set lowers it to the ground below slower than the fall impact
/// threshold. Removed automatically once the descent touches ground.
/// </summary>
[RegisterComponent, Access(typeof(CMUSharedZLevelsSystem))]
public sealed partial class CMUZHoverComponent : Component
{
    [DataField]
    public EntityUid TargetMap;

    /// <summary>
    /// Height within <see cref="TargetMap"/> to hold at.
    /// </summary>
    [DataField]
    public float HoverLocalPosition;

    [DataField]
    public float AscendSpeed = 2.5f;

    /// <summary>
    /// Must remain below the z-physics impact threshold so the landing is harmless.
    /// </summary>
    [DataField]
    public float DescendSpeed = 1.5f;

    [DataField]
    public bool Descending;

    /// <summary>
    /// Receives <see cref="CMUZHoverEndedEvent"/> when the hover is removed.
    /// </summary>
    [DataField]
    public EntityUid? Source;
}

/// <summary>
/// Raised on <see cref="CMUZHoverComponent.Source"/> after the hovering entity has landed or the hover was cancelled.
/// </summary>
[ByRefEvent]
public readonly record struct CMUZHoverEndedEvent(EntityUid Hoverer);
