using Robust.Shared.GameStates;

namespace Content.Shared.IgnitionSource;

/// <summary>
/// This is used for creating atmosphere hotspots while ignited to start reactions such as fire.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedIgnitionSourceSystem))]
public sealed partial class IgnitionSourceComponent : Component
{
    /// <summary>
    /// Is this source currently ignited?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Ignited;

    /// <summary>
    /// The temperature used when creating atmos hotspots.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Temperature = 700f;

    /// <summary>
    /// Server-side HotspotExpose throttle; the first expose fires immediately (zero default).
    /// </summary>
    // CMU14: IgnitionSourceSystem re-exposes at 1 Hz instead of every tick.
    [ViewVariables]
    public TimeSpan NextExpose;
}
