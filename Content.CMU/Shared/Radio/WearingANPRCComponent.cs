namespace Content.Shared.CMU14.Radio;

[RegisterComponent]
public sealed partial class WearingANPRCComponent : Component
{
    [DataField]
    public EntityUid Radio;

    [DataField]
    public bool PendingANPRCTransmit;
}
