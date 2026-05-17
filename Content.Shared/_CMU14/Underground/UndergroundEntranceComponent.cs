using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Placed on tunnel entrance entities (both surface and underground sides).
/// Stores a reference to the paired entrance on the other map.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundEntranceComponent : Component
{
    /// <summary>
    /// The paired entrance entity on the other map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Other;

    /// <summary>
    /// Time delay before the player is teleported through the entrance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether this entrance is on the underground side (true) or the surface side (false).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsUnderground;
}
