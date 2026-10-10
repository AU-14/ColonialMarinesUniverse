using Robust.Shared.Audio;
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

    // CMU14 Begin: per-prototype dog kit. Defaults keep the synth K9 behavior;
    // the yautja hellhound takes a beast's subset with its own voice.

    /// <summary>
    /// The dog's bond is assigned on spawn with its own senses: no Request Bond or Track Owner actions.
    /// </summary>
    [DataField]
    public bool PresetBond;

    /// <summary>
    /// Periodic scan for nearby xenos that warns the master.
    /// </summary>
    [DataField]
    public bool AcousticSenses = true;

    /// <summary>
    /// The senses warning reaches only the dog and its master, with no world sound.
    /// For hunting beasts whose masters hunt unseen.
    /// </summary>
    [DataField]
    public bool SilentSensesAlert;

    /// <summary>
    /// Landing melee hits spawns a headbite effect on the victim. Synthetic jaws only.
    /// </summary>
    [DataField]
    public bool HeadbiteStrikes = true;

    /// <summary>
    /// Feeding the dog a power cell triggers praise. Synthetic dogs only.
    /// </summary>
    [DataField]
    public bool BatteryTrick = true;

    /// <summary>
    /// Praise popup override for this dog's voice.
    /// </summary>
    [DataField]
    public LocId? PraiseMessage;
    [DataField]
    public SoundSpecifier? PraiseSound;

    /// <summary>
    /// Popup the master receives when the bond is set.
    /// </summary>
    [DataField]
    public LocId? BindMessageMaster;

    /// <summary>
    /// Popup the dog receives when the bond is set.
    /// </summary>
    [DataField]
    public LocId? BindMessageDog;

    /// <summary>
    /// Popup a grabbed victim sees while struggling free.
    /// </summary>
    [DataField]
    public LocId? GrabEscapeMessage;

    // CMU14 End
}
