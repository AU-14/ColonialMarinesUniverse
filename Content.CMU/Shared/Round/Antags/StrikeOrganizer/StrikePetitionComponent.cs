using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.StrikeOrganizer;

/// <summary>
/// A strike petition: the recruiting instrument of a venture. The venture itself
/// (grievance, stances, participants) lives on the rule, so a burned or confiscated
/// paper erases nothing; the organizer can always redraft one.
/// </summary>
[RegisterComponent]
public sealed partial class StrikePetitionComponent : Component
{
    /// <summary>
    /// Departments whose members refuse to sign a strike petition.
    /// </summary>
    [DataField]
    public List<ProtoId<DepartmentPrototype>> BlockedDepartments = new()
    {
        "AU14DepartmentColonyCommand",
        "AU14DepartmentGovernmentForces",
    };

    /// <summary>
    /// Duration of the signing do-after in seconds.
    /// </summary>
    [DataField]
    public float SignDoAfterDuration = 4f;
}
