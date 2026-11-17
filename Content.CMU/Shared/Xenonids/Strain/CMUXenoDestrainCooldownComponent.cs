using Content.Shared._RMC14.Xenonids.Evolution;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Xenonids.Strain;

/// <summary>Tracks the destraining cooldown across changes to a xeno's body.</summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(XenoEvolutionSystem))]
public sealed partial class CMUXenoDestrainCooldownComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(30);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDestrainAt;
}
