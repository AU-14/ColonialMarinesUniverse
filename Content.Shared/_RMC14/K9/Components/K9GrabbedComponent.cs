using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.K9.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class K9GrabbedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Dog;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 0.05f;

    [DataField, AutoNetworkedField]
    public TimeSpan GrabTime;

    /// <summary>
    /// Escape duration when victim attempts to resist.
    /// </summary>
    [DataField]
    public TimeSpan EscapeDuration = TimeSpan.FromSeconds(4.0);

    /// <summary>
    /// If an escape attempt DoAfter is already in progress.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Escaping;
}
