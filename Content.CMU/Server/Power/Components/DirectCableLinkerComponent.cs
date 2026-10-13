namespace Content.Server.Power.Components;

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
