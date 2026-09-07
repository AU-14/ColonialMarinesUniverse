using Content.Shared.CMU14.Chemistry.Reagents;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Chemistry.Research;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ResearchDataTerminalComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Clearance = 1;
    [DataField, AutoNetworkedField]
    public bool SyncClearance = false; // for 'public' terminals
}
