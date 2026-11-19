using Content.Shared.Camera;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.LawEnforcement;

/// <summary>
/// A camera that can be fitted to a vehicle. Once fitted it streams from the vehicle to <see cref="Network"/>,
/// listed under the vehicle's name. Taking it off needs one of <see cref="AuthorizedIds"/> and a screwdriver, or
/// shorting it out with a multitool first, which stops the stream for good.
/// </summary>
[RegisterComponent, Access(typeof(CMUDashCameraSystem))]
public sealed partial class CMUDashCameraComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CameraNetworkPrototype> Network;

    [DataField]
    public TimeSpan MountDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan RemoveDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ShortDelay = TimeSpan.FromSeconds(6);

    /// <summary>ID cards that unlock the mount so the camera can be unscrewed.</summary>
    [DataField]
    public HashSet<EntProtoId> AuthorizedIds = new();

    [ViewVariables]
    public EntityUid? Vehicle;

    /// <summary>Shorted out with a multitool: off the network, and anyone can unscrew it.</summary>
    [ViewVariables]
    public bool Shorted;
}

/// <summary>A vehicle with a dash camera fitted.</summary>
[RegisterComponent, Access(typeof(CMUDashCameraSystem))]
public sealed partial class CMUDashCameraMountComponent : Component
{
    public const string ContainerId = "cmu_dash_camera";

    [ViewVariables]
    public EntityUid Camera;
}
