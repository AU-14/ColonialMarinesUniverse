using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo;

/// <summary>
/// Gives the mind controlling this Wendigo the <see cref="Job"/> job, so its playtime is tracked as a Wendigo
/// instead of the generic threat job it was spawned with. Applied by the server's CMUWendigoJobSystem.
/// </summary>
[RegisterComponent]
public sealed partial class CMUWendigoJobComponent : Component
{
    [DataField]
    public ProtoId<JobPrototype> Job = "CMXenoWendigo";
}
