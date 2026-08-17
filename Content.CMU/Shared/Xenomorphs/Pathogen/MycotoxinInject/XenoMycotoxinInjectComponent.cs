using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.MycotoxinInject;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoMycotoxinInjectSystem))]
public sealed partial class CMUXenoMycotoxinInjectComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlasmaCost = 100f;

    [DataField, AutoNetworkedField]
    public float Range = 2.5f;

    [DataField, AutoNetworkedField]
    public bool CanInjectLiving = false;

    /// <summary>How long the tail-stab channel takes before the target reanimates.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);
}