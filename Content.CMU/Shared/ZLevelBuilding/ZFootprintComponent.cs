namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// A structure that covers more than one tile. It is anchored on its origin, the bottom-left tile, and covers
/// <see cref="Size"/> tiles from there (fixed facing; rotation is not applied). While it is anchored, the server
/// places an invisible <see cref="ZFootprintCellComponent"/> marker on every other covered tile, so a collapse of any
/// covered tile takes the whole structure down once, instead of leaving it floating over the hole.
/// </summary>
[RegisterComponent]
public sealed partial class ZFootprintComponent : Component
{
    /// <summary>Tiles covered, counted from the origin tile towards +X and +Y.</summary>
    [DataField]
    public Vector2i Size = Vector2i.One;

    /// <summary>The cell markers on the covered non-origin tiles. Server runtime state.</summary>
    [ViewVariables]
    public readonly List<EntityUid> Cells = new();

    /// <summary>The grid and origin tile the cells were placed for, while anchored. Server runtime state.</summary>
    [ViewVariables]
    public EntityUid? Grid;

    [ViewVariables]
    public Vector2i Origin;
}

/// <summary>
/// An invisible anchored marker on a covered non-origin tile of a <see cref="ZFootprintComponent"/> structure. When
/// its tile collapses, the owner falls (or is destroyed) with it; the marker itself is destroyed with the floor.
/// </summary>
[RegisterComponent]
public sealed partial class ZFootprintCellComponent : Component
{
    [ViewVariables]
    public EntityUid Owner;
}
