using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// On rad-shielded suits. Rad shielding only covers a head that is itself
/// rad-shielded: an exposed (or unrated) head gives part of the suit's dose
/// reduction back. See <see cref="CMURadProtectionSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMURadCoverageComponent : Component
{
    /// <summary>
    /// Multiplier added back to GetRadProtectionEvent while the head is not
    /// rad-shielded, i.e. how much of the suit's dose reduction an uncovered
    /// head costs.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UncoveredHeadPenalty = 0.4f;

    /// <summary>
    /// Minimum CMURadProtection reduction a head item needs to count as
    /// rad-shielded.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinHeadReduction = 0.1f;
}
