using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Underground.BurrowerTunnel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class BurrowerTunnelChoiceComponent : Component
{
    [DataField, AutoNetworkedField]
    public BurrowerTunnelType? Choice;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextHiveTunnelAt;
}

[Serializable, NetSerializable]
public enum BurrowerTunnelType : byte
{
    HiveTunnel,
    UndergroundEntrance,
}
