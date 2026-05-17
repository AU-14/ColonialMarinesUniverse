using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Marker component on rock entities in the underground map.
/// Used to identify rocks for the procedural generation system.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundRockComponent : Component
{
}
