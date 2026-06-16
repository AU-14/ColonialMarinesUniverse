using Content.Shared._AU14.Chemistry.Reagents;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class ResearchDataTerminalComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Clearance = 1;
    [DataField, AutoNetworkedField]
    public int BasePurchaseCost = 5;
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(360); // six minutes
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastPickAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPickAt;
    [DataField, AutoNetworkedField]
    public HashSet<GeneratedReagentData> DisplayReagents = [];
}
