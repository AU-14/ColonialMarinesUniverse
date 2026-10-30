using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Spawners;

/// <summary>
///     Lets a job spawn point take more jobs than the one it's for, so a new job can use an existing job's spawn points
///     on every map instead of needing its own placed on each one.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSpawnPointExtraJobsComponent : Component
{
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();
}
