using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Camera;

[Serializable, NetSerializable]
public enum RMCCameraUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RMCCameraWatchBuiMsg(NetEntity camera) : BoundUserInterfaceMessage
{
    public readonly NetEntity Camera = camera;
}

[Serializable, NetSerializable]
public sealed class RMCCameraPreviousBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RMCCameraNextBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RMCCameraRefreshSubnetsBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RMCCameraSessionNetworkBuiMsg(NetEntity network) : BoundUserInterfaceMessage
{
    public NetEntity Network { get; } = network;
}

[Serializable, NetSerializable]
public sealed class RMCCameraEditorStateBuiMsg(
    bool enabled,
    RMCCameraNetworkEditorUiState state) : BoundUserInterfaceMessage
{
    public bool Enabled { get; } = enabled;
    public RMCCameraNetworkEditorUiState State { get; } = state;
}

[Serializable, NetSerializable]
public sealed class RMCCameraDisconnectBuiMsg : BoundUserInterfaceMessage;
