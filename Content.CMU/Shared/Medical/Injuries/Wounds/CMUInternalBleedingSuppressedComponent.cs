namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

[RegisterComponent]
[Access(typeof(SharedCMUWoundsSystem))]
public sealed partial class CMUInternalBleedingSuppressedComponent : Component
{
    [DataField]
    public string Source = string.Empty;

    [DataField]
    public float BloodlossPerSecond;

    /// <summary>
    ///     When a drug's hold on the bleed wears off and it starts bleeding again. Null for a surgical clamp, which
    ///     holds for good.
    /// </summary>
    [DataField]
    public TimeSpan? ExpiresAt;
}
