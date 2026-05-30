using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Hive;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoHiveSystem))]
public sealed partial class HiveMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Hive;
}

[Serializable, NetSerializable]
public enum XenoHiveVisuals
{
    Color,
}
