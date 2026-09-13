using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Camera;

[Serializable, NetSerializable]
public enum RMCCameraNetworkEditorOrigin : byte
{
    Seeded,
    Owned,
}

[Serializable, NetSerializable]
public enum RMCCameraNetworkEditorError : byte
{
    None,
    AccessDenied,
    StaleRevision,
    InvalidName,
    DuplicateName,
    MissingCamera,
    InvalidNetwork,
    SeededNetworkCannotBeDeleted,
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorNetworkUiData(
    NetEntity id,
    string name,
    RMCCameraNetworkEditorOrigin origin,
    bool hidden)
{
    public NetEntity Id { get; } = id;
    public string Name { get; } = name;
    public RMCCameraNetworkEditorOrigin Origin { get; } = origin;
    public bool Hidden { get; } = hidden;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorCameraUiData(
    NetEntity camera,
    string name,
    List<NetEntity> networks)
{
    public NetEntity Camera { get; } = camera;
    public string Name { get; } = name;
    public List<NetEntity> Networks { get; } = networks;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorUiState(
    uint revision,
    List<RMCCameraNetworkEditorNetworkUiData> networks,
    List<RMCCameraNetworkEditorCameraUiData> cameras)
{
    public uint Revision { get; } = revision;
    public List<RMCCameraNetworkEditorNetworkUiData> Networks { get; } = networks;
    public List<RMCCameraNetworkEditorCameraUiData> Cameras { get; } = cameras;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorCreateBuiMsg(uint revision, string name) : BoundUserInterfaceMessage
{
    public uint Revision { get; } = revision;
    public string Name { get; } = name;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorRenameBuiMsg(
    uint revision,
    NetEntity network,
    string name) : BoundUserInterfaceMessage
{
    public uint Revision { get; } = revision;
    public NetEntity Network { get; } = network;
    public string Name { get; } = name;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorDeleteBuiMsg(
    uint revision,
    NetEntity network) : BoundUserInterfaceMessage
{
    public uint Revision { get; } = revision;
    public NetEntity Network { get; } = network;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorSetHiddenBuiMsg(
    uint revision,
    NetEntity network,
    bool hidden) : BoundUserInterfaceMessage
{
    public uint Revision { get; } = revision;
    public NetEntity Network { get; } = network;
    public bool Hidden { get; } = hidden;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorSaveCameraBuiMsg(
    uint revision,
    NetEntity camera,
    string name,
    List<NetEntity> networks) : BoundUserInterfaceMessage
{
    public uint Revision { get; } = revision;
    public NetEntity Camera { get; } = camera;
    public string Name { get; } = name;
    public List<NetEntity> Networks { get; } = networks;
}

[Serializable, NetSerializable]
public sealed class RMCCameraNetworkEditorResultBuiMsg(
    RMCCameraNetworkEditorError error,
    uint revision) : BoundUserInterfaceMessage
{
    public RMCCameraNetworkEditorError Error { get; } = error;
    public uint Revision { get; } = revision;
}
