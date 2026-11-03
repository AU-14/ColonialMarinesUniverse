namespace Content.Server.CMU14.Round;

/// <summary>
/// Deletes the entity at round start in the listed gamemode presets.
/// Server only: the entity never reaches the client in blacklisted modes.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGamemodeBlacklistComponent : Component
{
    [DataField]
    public List<string> BlacklistedGamemodes = new();
}
