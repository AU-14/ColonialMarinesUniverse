using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.K9.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class K9DogComponent : Component
{
    /// <summary>
    /// The marine or handler this dog is bonded with (access + tracking).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    /// <summary>
    /// True when the current bond is with a K9 handler (commands apply).
    /// False when bonded to an ordinary marine (access and tracking only).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HandlerBonded;

    /// <summary>
    /// The humanoid currently held by arm grab.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? GrabbedTarget;

    /// <summary>
    /// Action prototype for "Arm Grab".
    /// </summary>
    [DataField]
    public EntProtoId ArmGrabActionId = "RMCActionK9ArmGrab";

    [DataField, AutoNetworkedField]
    public EntityUid? ArmGrabAction;

    /// <summary>
    /// Action prototype for "Find Master".
    /// </summary>
    [DataField]
    public EntProtoId TrackMasterActionId = "RMCActionK9TrackMaster";

    [DataField, AutoNetworkedField]
    public EntityUid? TrackMasterAction;

    /// <summary>
    /// Action prototype for "Request Master" (used when dog has no handler).
    /// </summary>
    [DataField]
    public EntProtoId RequestMasterActionId = "RMCActionK9RequestMaster";

    [DataField, AutoNetworkedField]
    public EntityUid? RequestMasterAction;

    /// <summary>
    /// Next time acoustic sensors scan the surroundings for hidden enemies.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextSensesScan = TimeSpan.Zero;

    [DataField]
    public TimeSpan SensesScanInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Next time acoustic sensors are allowed to broadcast an alert.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextSensesAlert = TimeSpan.Zero;

    [DataField]
    public TimeSpan SensesAlertCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public float SensesRange = 9.0f;
}
