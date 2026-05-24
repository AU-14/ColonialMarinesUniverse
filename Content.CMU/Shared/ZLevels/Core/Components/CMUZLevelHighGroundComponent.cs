using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Allows entities not to fall if they are above this entity at a higher level.
/// Think of it as the ability to walk on top of walls, for example.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUZLevelHighGroundComponent : Component
{
    /// <summary>
    /// Height profile points, forming a simple curve (0..1 by X, height by Y).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<float> HeightCurve = new()
    {
        1.05f,
        1.05f,
    };

    /// <summary>
    /// Forcibly attaches the entity to itself along the z-axis if the character descends smoothly. Needed for prevent falling from staircases.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Stick = false;

    /// <summary>
    /// If true, this high ground only supports entities checking from a higher Z-level.
    /// Useful for ladders: the base can hold someone at the opening above without auto-stepping someone on the lower tile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SupportOnlyFromAbove = false;

    /// <summary>
    /// Allows this highground to automatically reveal a nearby preview of the level above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PreviewUpLevel = true;

    /// <summary>
    /// Maximum distance in tiles/world units at which this highground can reveal the level above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PreviewRange = 5f;

    /// <summary>
    /// TODO: Workaround for the inability to place map entities rotated by 45 degrees.
    /// When fixed, this flag should be removed in favor of proper rotation support.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Corner = false;
}
