using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
/// A pay tier for the money a character starts with in their ID card's bank account.
/// The highest priority tier that lists the job, or one of its departments, wins; otherwise the default tier applies.
/// </summary>
[Prototype("cmuStartingFunds")]
public sealed partial class CMUStartingFundsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int Min;

    [DataField(required: true)]
    public int Max;

    [DataField]
    public int Priority;

    [DataField]
    public bool Default;

    [DataField]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> Departments = new();
}
