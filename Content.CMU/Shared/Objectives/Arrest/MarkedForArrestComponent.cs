using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Arrest;

/// <summary>
/// Marker component to track entities that are arrest objective targets.
/// Maps objective entity UIDs to the faction that should get credit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MarkedForArrestComponent : Component
{
    /// <summary>
    /// Maps objective entity UID to the faction that should get credit for the arrest.
    /// </summary>
    public Dictionary<EntityUid, string> AssociatedObjectives { get; set; } = new();

    /// <summary>
    /// Maps objective entity UID to cached job ID (if SpecificJob is used).
    /// </summary>
    public Dictionary<EntityUid, string?> AssociatedObjectiveJobs { get; set; } = new();
}

