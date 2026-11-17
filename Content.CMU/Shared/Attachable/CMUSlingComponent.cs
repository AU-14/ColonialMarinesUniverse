using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Attachable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUSlingSystem))]
public sealed partial class CMUSlingComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUSlingMount Mount = CMUSlingMount.Sling;

    [DataField, AutoNetworkedField]
    public CMUSlingCarryMode CarryMode = CMUSlingCarryMode.SuitStorage;
}

[Serializable, NetSerializable]
public enum CMUSlingMount : byte
{
    Sling,
    Rail,
    Underbarrel,
}

[Serializable, NetSerializable]
public enum CMUSlingCarryMode : byte
{
    SuitStorage,
    Vest,
}
