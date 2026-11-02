namespace Content.Server.Power.Components;

// A tool (coil) that runs a cable by hand from one device to another, up to MaxDistance; remembers the first device it was used on.
[RegisterComponent]
public sealed partial class DirectCableLinkerComponent : Component
{
    [DataField]
    public float MaxDistance = 15f;

    [ViewVariables]
    public EntityUid? PendingSource;

    [ViewVariables]
    public string? PendingNodeName;
}
