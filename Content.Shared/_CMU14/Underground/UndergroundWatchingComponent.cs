using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Added to entities currently peeking through an underground entrance.
/// Removed when the entity moves, breaking the peek.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundWatchingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;
}
