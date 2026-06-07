using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoConstructionSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class XenoHiveInstantBuildActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public int BuildsLeft;

    [DataField, AutoNetworkedField]
    public bool Visible;
}
