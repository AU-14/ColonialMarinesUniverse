namespace Content.Server._AU14.Insurgency;

/// <summary>
/// Server-owned delivery state for faction effects applied to a spawned member.
/// Kept on the mob so repeated lifecycle events are harmless while a respawned mob is handled normally.
/// </summary>
[RegisterComponent]
public sealed partial class InsurgencyFactionApplicationComponent : Component
{
    public uint BriefingGeneration;
    public bool PackageGranted;
}
