using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground.Markers;

/// <summary>
/// Placed on the surface map by mappers. When the underground is created,
/// the matching underground tile is cleared (dug marker placed, no rock spawned).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UndergroundPreDigMarkerComponent : Component;
