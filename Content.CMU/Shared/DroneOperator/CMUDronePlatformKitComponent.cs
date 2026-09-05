using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DroneOperator;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUDronePlatformKitComponent : Component
{
    [DataField]
    public EntProtoId HumanoidPack = "CMUDroneOperatorPackFilled";

    [DataField]
    public EntProtoId TrackedPack = "CMUCombatDroneOperatorPackFilled";

    [DataField]
    public bool Claimed;
}

[Serializable, NetSerializable]
public enum CMUDronePlatform : byte
{
    Humanoid,
    Tracked,
}

[Serializable, NetSerializable]
public enum CMUDronePlatformKitUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUDronePlatformSelectedMessage(CMUDronePlatform platform) : BoundUserInterfaceMessage
{
    public readonly CMUDronePlatform Platform = platform;
}
