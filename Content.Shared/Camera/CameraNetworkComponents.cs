using Robust.Shared.Prototypes;

namespace Content.Shared.Camera;

[Flags]
public enum CameraSourceKinds : byte
{
    None = 0,
    Standard = 1 << 0,
    Rmc = 1 << 1,
    All = Standard | Rmc,
}

/// <summary>
/// A round-scoped logical camera network. Prototype IDs are only seeds used to
/// create these identities; runtime-created networks have no seed.
/// </summary>
[RegisterComponent]
public sealed partial class CameraNetworkIdentityComponent : Component
{
    [DataField] public ProtoId<CameraNetworkPrototype>? Seed;
    [DataField] public string DisplayName = string.Empty;
    [DataField] public EntityUid? CreatedBy;
    [DataField] public bool Runtime;
}

[RegisterComponent]
public sealed partial class CameraNetworkMemberComponent : Component
{
    [DataField(required: true)] public HashSet<ProtoId<CameraNetworkPrototype>> Networks = [];
    [DataField(required: true)] public CameraSourceKinds SourceKinds = CameraSourceKinds.None;

    /// <summary>
    /// Runtime network memberships. Static YAML memberships are resolved from
    /// <see cref="Networks"/> into canonical network entities by the server.
    /// </summary>
    public HashSet<EntityUid> RuntimeNetworks = [];
}

[RegisterComponent]
public sealed partial class CameraNetworkReceiverComponent : Component
{
    [DataField] public HashSet<ProtoId<CameraNetworkPrototype>> Networks = [];
    [DataField(required: true)] public CameraSourceKinds SupportedSources = CameraSourceKinds.None;

    public HashSet<EntityUid> RuntimeNetworks = [];
}

[RegisterComponent]
public sealed partial class CameraMapMarkerComponent : Component
{
    [DataField] public bool Visible = true;
    [DataField] public bool Mobile;
    [DataField] public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);
}
