namespace Content.Shared._CMU14.Medical.Wounds;

[RegisterComponent]
[Access(typeof(SharedCMUWoundsSystem))]
public sealed partial class CMUInternalBleedingSuppressedComponent : Component
{
    [DataField]
    public string Source = string.Empty;

    [DataField]
    public float BloodlossPerSecond;
}
