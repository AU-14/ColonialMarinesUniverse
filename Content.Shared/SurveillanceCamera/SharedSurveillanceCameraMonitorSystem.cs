using Content.Shared.Camera;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.SurveillanceCamera;

[Serializable, NetSerializable]
public sealed class CameraSessionNetworkUiData(NetEntity network, string name)
{
    public NetEntity Network { get; } = network;
    public string Name { get; } = name;
}

[Serializable, NetSerializable]
public sealed class CameraSessionCameraUiData(NetEntity camera, string name, bool active)
{
    public NetEntity Camera { get; } = camera;
    public string Name { get; } = name;
    public bool Active { get; } = active;
}

[Serializable, NetSerializable]
public sealed class CameraSessionDirectoryUiData(
    NetEntity? activeCamera,
    string? activeCameraName,
    List<CameraSessionNetworkUiData> networks,
    NetEntity? activeNetwork,
    List<CameraSessionCameraUiData> cameras,
    bool mapEnabled)
{
    public NetEntity? ActiveCamera { get; } = activeCamera;
    public string? ActiveCameraName { get; } = activeCameraName;
    public List<CameraSessionNetworkUiData> Networks { get; } = networks;
    public NetEntity? ActiveNetwork { get; } = activeNetwork;
    public List<CameraSessionCameraUiData> Cameras { get; } = cameras;
    public bool MapEnabled { get; } = mapEnabled;
}

[Serializable, NetSerializable]
public sealed class CameraSessionSnapshotMessage(
    uint sessionId,
    ulong revision,
    CameraSessionDirectoryUiData directory) : BoundUserInterfaceMessage
{
    public uint SessionId { get; } = sessionId;
    public ulong Revision { get; } = revision;
    public CameraSessionDirectoryUiData Directory { get; } = directory;
}

[Serializable, NetSerializable]
public sealed class CameraSessionDeltaMessage(
    uint sessionId,
    ulong baseRevision,
    ulong revision,
    CameraSessionDirectoryUiData directory) : BoundUserInterfaceMessage
{
    public uint SessionId { get; } = sessionId;
    public ulong BaseRevision { get; } = baseRevision;
    public ulong Revision { get; } = revision;
    public CameraSessionDirectoryUiData Directory { get; } = directory;
}

[Serializable, NetSerializable]
public sealed class CameraSessionGeometryMessage(
    uint sessionId,
    NetEntity? network,
    ulong markerRevision,
    CameraMapUiState geometry) : BoundUserInterfaceMessage
{
    public uint SessionId { get; } = sessionId;
    public NetEntity? Network { get; } = network;
    public ulong MarkerRevision { get; } = markerRevision;
    public CameraMapUiState Geometry { get; } = geometry;
}

[Serializable, NetSerializable]
public sealed class CameraSessionResetMessage(uint sessionId) : BoundUserInterfaceMessage
{
    public uint SessionId { get; } = sessionId;
}

[Serializable, NetSerializable]
public sealed class CameraSessionResyncMessage(uint sessionId) : BoundUserInterfaceMessage
{
    public uint SessionId { get; } = sessionId;
}

[Serializable, NetSerializable]
public sealed class CameraSessionSelectMessage(NetEntity camera) : BoundUserInterfaceMessage
{
    public NetEntity Camera { get; } = camera;
}

[Serializable, NetSerializable]
public sealed class CameraSessionSelectNetworkMessage(NetEntity network) : BoundUserInterfaceMessage
{
    public NetEntity Network { get; } = network;
}

[Serializable, NetSerializable]
public sealed class CameraSessionDisconnectMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public enum SurveillanceCameraMonitorUiKey : byte
{
    Key
}

// SETUP

[Serializable, NetSerializable]
public sealed class SurveillanceCameraSetupBoundUiState : BoundUserInterfaceState
{
    public string Name { get; }
    public uint Network { get; }
    public List<string> Networks { get; }
    public bool NameDisabled { get; }
    public bool NetworkDisabled { get; }

    public SurveillanceCameraSetupBoundUiState(string name, uint network, List<string> networks, bool nameDisabled, bool networkDisabled)
    {
        Name = name;
        Network = network;
        Networks = networks;
        NameDisabled = nameDisabled;
        NetworkDisabled = networkDisabled;
    }
}

[Serializable, NetSerializable]
public sealed class SurveillanceCameraLogicalNetworkSetupBoundUiState(
    string name,
    ProtoId<CameraNetworkPrototype>? network,
    List<ProtoId<CameraNetworkPrototype>> networks,
    bool nameDisabled,
    bool networkDisabled) : BoundUserInterfaceState
{
    public string Name { get; } = name;
    public ProtoId<CameraNetworkPrototype>? Network { get; } = network;
    public List<ProtoId<CameraNetworkPrototype>> Networks { get; } = networks;
    public bool NameDisabled { get; } = nameDisabled;
    public bool NetworkDisabled { get; } = networkDisabled;
}

[Serializable, NetSerializable]
public sealed class SurveillanceCameraSetupSetName : BoundUserInterfaceMessage
{
    public string Name { get; }

    public SurveillanceCameraSetupSetName(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class SurveillanceCameraSetupSetNetwork : BoundUserInterfaceMessage
{
    public int Network { get; }

    public SurveillanceCameraSetupSetNetwork(int network)
    {
        Network = network;
    }
}


[Serializable, NetSerializable]
public enum SurveillanceCameraSetupUiKey : byte
{
    Camera,
    Router
}
