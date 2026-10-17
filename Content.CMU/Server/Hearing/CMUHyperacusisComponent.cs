namespace Content.Server.CMU14.Hearing;

/// <summary>
///     Trait: combat noise damages this person's hearing faster.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHyperacusisComponent : Component
{
    [DataField]
    public float ExposureMultiplier = 2f;
}
