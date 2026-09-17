using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// On fire outerwear. Fire protection only covers a head that is itself fire-rated:
/// an exposed (or non-rated) head loses the suit's ignition immunity and part of its
/// burn reduction. See <see cref="CMUFireCoverageSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUFireCoverageComponent : Component
{
    /// <summary>
    /// Multiplier added back to GetFireProtectionEvent while the head is not fire-rated,
    /// i.e. how much of the suit's burn reduction an uncovered head costs.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UncoveredHeadPenalty = 0.6f;

    /// <summary>
    /// Minimum FireProtection reduction a head item needs to count as a rated head.
    /// Token protection (combat helmets, hard hats at 0.05) eases the burn but does
    /// not restore the suit's rating; the fire helmets at 0.15+ do.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinHeadReduction = 0.1f;
}
