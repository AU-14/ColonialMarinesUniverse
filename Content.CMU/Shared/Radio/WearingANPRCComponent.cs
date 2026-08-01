namespace Content.Shared._AU14.Radio;

[RegisterComponent]
public sealed partial class WearingANPRCComponent : Component
{
    [DataField]
    public EntityUid Radio;

    [DataField]
    public bool PendingANPRCTransmit;
}
