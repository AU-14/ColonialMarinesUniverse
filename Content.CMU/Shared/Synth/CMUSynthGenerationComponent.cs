using Content.Shared._RMC14.Synth;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSynthSystem))]
public sealed partial class CMUSynthGenerationComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Generation = 1;
}
