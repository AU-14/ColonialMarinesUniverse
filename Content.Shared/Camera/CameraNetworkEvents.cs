using Robust.Shared.Prototypes;

namespace Content.Shared.Camera;

public enum CameraReceiverChangeKind : byte
{
    Authorization,
    MemberList,
    Directory,
    Marker,
}

[ByRefEvent]
public record struct CameraReceiverChangedEvent(CameraReceiverChangeKind Kind, EntityUid? Camera = null);

[ByRefEvent]
public record struct CameraNetworkGrantRequestEvent(
    ProtoId<CameraNetworkPrototype> Network,
    EntityUid Source,
    bool Grant);

[Flags]
public enum CameraSessionCapabilities : byte
{
    None = 0,
    Browse = 1 << 0,
    LiveView = 1 << 1,
    Map = 1 << 2,
}

[ByRefEvent]
public record struct CameraSessionChangedEvent(EntityUid Actor);
