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

    [DataField]
    public Dictionary<CMUExercise, float> Fatigue = new();

    [DataField]
    public Dictionary<CMUExercise, TimeSpan> FatigueSetAt = new();

    [DataField]
    public TimeSpan FatigueRecovery = TimeSpan.FromMinutes(5);

    [DataField]
    public float MoveCancelDistance = 0.3f;
}
