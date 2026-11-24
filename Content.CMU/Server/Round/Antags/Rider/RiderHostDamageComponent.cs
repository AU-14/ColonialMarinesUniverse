namespace Content.Server.CMU14.Round.Antags.Rider;

/// <summary>
/// Keeps a potential host's latest injury time across latching.
/// </summary>
[RegisterComponent, Access(typeof(RiderSystem))]
public sealed partial class RiderHostDamageComponent : Component
{
    public TimeSpan LastDamageAt;
}
