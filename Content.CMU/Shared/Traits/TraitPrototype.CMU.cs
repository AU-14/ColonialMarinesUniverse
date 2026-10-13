// ReSharper disable CheckNamespace

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Traits;

public sealed partial class TraitPrototype
{
    /// <summary>
    ///     Jobs in these departments never get this trait, even if the character has it picked.
    /// </summary>
    [DataField("cmuBlockedDepartments")]
    public List<ProtoId<DepartmentPrototype>> CMUBlockedDepartments { get; private set; } = new();

    /// <summary>
    ///     Whether this trait is kept off the given job by <see cref="CMUBlockedDepartments"/>.
    /// </summary>
    public bool CMUIsBlockedForJob(IPrototypeManager prototypes, ProtoId<JobPrototype> job)
    {
        foreach (var departmentId in CMUBlockedDepartments)
        {
            if (prototypes.TryIndex(departmentId, out var department) && department.Roles.Contains(job))
                return true;
        }

        return false;
    }
}
