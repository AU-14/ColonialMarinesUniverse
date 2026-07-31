namespace Content.Shared._AU14.Radio;

// the physical corded handset of an ANPRC manpack. lives in a container slot on the
// pack, taking it puts it in a hand, hanging up or breaking the cord snaps it back
[RegisterComponent]
public sealed partial class ANPRCHandsetComponent : Component
{
    // server-side, the pack this handset is wired into
    [DataField]
    public EntityUid? Radio;
}
