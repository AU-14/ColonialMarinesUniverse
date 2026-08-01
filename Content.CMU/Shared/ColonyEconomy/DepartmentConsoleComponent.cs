using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.ColonyEconomy;

[RegisterComponent]
public sealed partial class DepartmentConsoleComponent : Component
{
    [DataField("departmentName")]
    public string DepartmentName = "Department";

    /// <summary>
    ///     The prototype ID of the department this console manages.
    ///     When set, round-start (and late-join) players whose job is in this department
    ///     are automatically added as members.
    /// </summary>
    [DataField("departmentId")]
    public ProtoId<DepartmentPrototype>? DepartmentId;

    /// <summary>
    ///     The access level used to lock crates ordered through this department console.
    /// </summary>
    [DataField("departmentAccessLevel")]
    public ProtoId<AccessLevelPrototype>? DepartmentAccessLevel;

    [DataField("defaultSalary")]
    public int DefaultSalary = 50;

    /// <summary>
    ///     Whether this department is managed by the colony budget console.
    ///     When false, the department won't appear in the budget console's department list
    ///     and can't receive transfers from the colony budget, but still functions independently.
    /// </summary>
    [DataField("colonyManaged")]
    public bool ColonyManaged = true;

    /// <summary>
    ///     The ASRS faction this department console orders from.
    ///     Defaults to "colony". Set to "corporate" to route orders through the corporate ASRS.
    ///     When set to "corporate", a sales tax is applied and the tax revenue goes to the colony budget.
    /// </summary>
    [DataField("asrsFaction")]
    public string AsrsFaction = "colony";

    /// <summary>
    ///     Budget for this department, filled via budget console transfers or cash insertion.
    /// </summary>
    public float DepartmentBudget;

    /// <summary>
    ///     Set of ID card entity UIDs that are members of this department.
    /// </summary>
    public HashSet<EntityUid> Members = new();

    /// <summary>
    ///     Individual salary overrides keyed by ID card entity UID.
    ///     Takes precedence over DefaultSalary.
    /// </summary>
    public Dictionary<EntityUid, int> SalaryOverrides = new();
}
