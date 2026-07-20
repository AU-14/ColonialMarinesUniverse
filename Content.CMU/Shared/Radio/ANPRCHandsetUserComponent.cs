namespace Content.Shared._AU14.Radio;

[RegisterComponent]
public sealed partial class ANPRCHandsetUserComponent : Component
{
    [DataField]
    public EntityUid Radio;

    public bool PendingTransmit;

    public readonly HashSet<string> GrantedChannels = new();

    public bool AddedIntrinsicReceiver;
}
