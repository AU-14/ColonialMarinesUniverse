using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMycotoxinSystem))]
public sealed partial class MycotoxinExposureComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Exposure;

    [DataField, AutoNetworkedField]
    public float DepletionPerTick = 1f;

    [DataField, AutoNetworkedField]
    public float InfectThreshold = 30f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextTickAt;

    [DataField, AutoNetworkedField]
    public TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public bool Infected;

    /// <summary>
    /// Set from the injector on first exposure. What VictimInfectedComponent
    /// will spawn once Mycotoxin crosses InfectThreshold.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId EmbryoSpawn = "CMU14XenoBloodburster";

    /// <summary>
    /// Hive assigned to the resulting VictimInfectedComponent, set from
    /// whichever cloud/injector first exposed this victim.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceHive;

    [DataField, AutoNetworkedField]
    public bool StrongEffects = false;
}