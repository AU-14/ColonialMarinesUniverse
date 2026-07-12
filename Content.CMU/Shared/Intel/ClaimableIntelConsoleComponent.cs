using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Intel;

/// <summary>
/// Allows a configured faction to seize an enemy intel console and claim an objective victory.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClaimableIntelConsoleComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string ClaimingTeam = string.Empty;

    [DataField, AutoNetworkedField]
    public string? RequiredPreset;

    [DataField, AutoNetworkedField]
    public TimeSpan ClaimTime = TimeSpan.FromSeconds(10);

    [AutoNetworkedField]
    public bool Claimed;

    /// <summary>
    /// Returns whether the supplied team may begin claiming this console.
    /// </summary>
    public bool CanStartClaim(string? team)
    {
        return !Claimed &&
               !string.IsNullOrWhiteSpace(team) &&
               string.Equals(team, ClaimingTeam, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Revalidates the claiming team and active preset before granting victory.
    /// </summary>
    public bool CanCompleteClaim(string? team, string? preset)
    {
        if (!CanStartClaim(team))
            return false;

        return string.IsNullOrWhiteSpace(RequiredPreset) ||
               string.Equals(preset, RequiredPreset, StringComparison.OrdinalIgnoreCase);
    }
}
