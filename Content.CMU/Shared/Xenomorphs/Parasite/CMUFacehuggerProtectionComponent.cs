using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Parasite;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoParasiteSystem))]
public sealed partial class CMUFacehuggerProtectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Infinite;

    [DataField, AutoNetworkedField]
    public int MaxUses = 1;

    [DataField, AutoNetworkedField]
    public int Uses;
}
