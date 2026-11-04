using Content.Shared.CMU14.Pushups;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Gym;

/// <summary>
///     A weight machine. Using it puts you on it and has you do reps until your body gives out, the same way pushups do.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUExerciseMachineComponent : Component
{
    [DataField(required: true)]
    public CMUExercise Exercise;

    [DataField, AutoNetworkedField]
    public EntityUid? User;

    [DataField]
    public SoundSpecifier? RepSound = new SoundPathSpecifier("/Audio/CMU14/Effects/spring.ogg", AudioParams.Default.WithVolume(-4f));

    /// <summary>A sprite state drawn over the person using the machine, like a barbell.</summary>
    [DataField]
    public string? UserOverlayState;
}

[Serializable, NetSerializable]
public enum CMUExerciseMachineVisuals : byte
{
    InUse,
}

/// <summary>
///     Lets you curl it while it's in your hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUDumbbellComponent : Component
{
    /// <summary>How many more or fewer curls someone manages with this weight than with a standard one.</summary>
    [DataField]
    public float RepMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMUPunchingBagComponent : Component
{
    [DataField]
    public SoundSpecifier HitSound = new SoundCollectionSpecifier("Punch", AudioParams.Default.WithVolume(-4f).WithVariation(0.05f));

    [DataField]
    public TimeSpan SwingTime = TimeSpan.FromSeconds(1.1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan SwingUntil;

    /// <summary>How long someone who punched themselves out has to rest before they can punch it again.</summary>
    [DataField]
    public TimeSpan TiredTime = TimeSpan.FromSeconds(30);
}

[Serializable, NetSerializable]
public enum CMUPunchingBagVisuals : byte
{
    Swinging,
}

/// <summary>
///     One half of a treadmill. The console half turns the belt on and off and spawns the back half behind it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUTreadmillComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Running;

    [DataField, AutoNetworkedField]
    public EntityUid? Other;

    /// <summary>The back half to spawn behind this one, if this is the console.</summary>
    [DataField]
    public EntProtoId? BackHalf;

    /// <summary>Drawn over the runner, so the handles go in front of them.</summary>
    [DataField]
    public EntProtoId? Handles;

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg");
}

[Serializable, NetSerializable]
public enum CMUTreadmillVisuals : byte
{
    Running,
}
