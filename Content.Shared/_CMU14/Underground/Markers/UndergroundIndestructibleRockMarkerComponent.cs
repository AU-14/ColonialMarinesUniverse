using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground.Markers;

/// <summary>
/// Placed on the surface map by mappers. When the underground is created,
/// the matching underground tile gets an indestructible border rock instead of
/// a normal mineable rock. Creates permanent walls inside the underground.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UndergroundIndestructibleRockMarkerComponent : Component;
