using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Attachable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUSlingSystem))]
public sealed partial class CMUSlingComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUSlingMount Mount = CMUSlingMount.Sling;
}

[Serializable, NetSerializable]
public enum CMUSlingMount : byte
{
    Sling,
    Rail,
    Underbarrel,
}
