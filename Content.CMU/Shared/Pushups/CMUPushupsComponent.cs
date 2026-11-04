using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Pushups;

[Serializable, NetSerializable]
public enum CMUExercise : byte
{
    Pushups,
    Situps,
    StackLifter,
    BenchPress,
    Curls,
    Punching,
    Running,
    KneePushups,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUPushupsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public CMUExercise Exercise;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan RepStart;

    [DataField, AutoNetworkedField]
    public TimeSpan RepDuration = TimeSpan.FromSeconds(1.3);

    [DataField]
    public TimeSpan BaseRepDuration = TimeSpan.FromSeconds(1.3);

    [DataField]
    public int Count;

    [DataField]
    public int Limit;

    [DataField]
    public int RestedLimit;

    [DataField]
    public EntityCoordinates Start;

    /// <summary>The machine or dumbbell being used, if the exercise needs one.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Equipment;

    /// <summary>When the current punching bag or treadmill session last saw any effort.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan SessionLast;

    [DataField]
    public CMUExercise? SessionExercise;

    /// <summary>Punches thrown, or seconds run, in the current session.</summary>
    [DataField]
    public float SessionAmount;

    [DataField]
    public int SessionLimit;

    [DataField]
    public int SessionRestedLimit;

    [DataField]
    public int SessionAnnounced;

    [DataField]
    public TimeSpan SessionTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    ///     After their arms give out on pushups, they stay down until this time so they can carry on with knee pushups.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? KneesOfferedUntil;

    [DataField]
    public TimeSpan KneesOfferTime = TimeSpan.FromSeconds(8);

    /// <summary>Their arms are too tired to punch a punching bag until this time.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? PunchTiredUntil;

    [DataField]
    public Dictionary<CMUExercise, float> Fatigue = new();

    [DataField]
    public Dictionary<CMUExercise, TimeSpan> FatigueSetAt = new();

    [DataField]
    public TimeSpan FatigueRecovery = TimeSpan.FromMinutes(5);

    [DataField]
    public float MoveCancelDistance = 0.3f;
}
