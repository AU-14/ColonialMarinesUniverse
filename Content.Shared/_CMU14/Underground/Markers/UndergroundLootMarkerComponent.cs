using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Underground.Markers;

/// <summary>
/// Placed on the surface map by mappers. When the underground is created,
/// the matching underground tile is pre-dug and an entity is spawned there.
/// Used for placing loot, resources, or points of interest underground.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UndergroundLootMarkerComponent : Component
{
    /// <summary>
    /// The entity prototype to spawn on the underground tile.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Spawn;
}
