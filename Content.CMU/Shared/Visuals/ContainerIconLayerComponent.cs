using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Visuals;

/// <summary>
///     Draws the icon of the most recently inserted item of one container as an extra sprite layer on
///     the owning entity, and hides the layer when the container is empty. Purely cosmetic, handled
///     client-side by ContainerIconLayerSystem. Used by the tripwire charge (attached explosive perched
///     on the box) and the Sapper's Workbench (the weapon lying on the tabletop).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AU14ContainerIconLayerComponent : Component
{
    /// <summary>The container whose newest item is shown.</summary>
    [DataField(required: true)]
    public string Container = string.Empty;

    /// <summary>Scale of the drawn icon relative to the item's normal sprite size.</summary>
    [DataField]
    public Vector2 Scale = Vector2.One;

    /// <summary>Offset of the drawn icon on the owner, in tiles.</summary>
    [DataField]
    public Vector2 Offset = Vector2.Zero;

    /// <summary>Optional tint over the drawn icon (e.g. to weather it into the owner's palette).</summary>
    [DataField]
    public Color? Tint;
}
