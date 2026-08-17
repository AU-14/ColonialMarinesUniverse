using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;

/// <summary>
/// Component for spore clouds (or other sources) that inject Mycotoxin into
/// anyone standing in contact with them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMycotoxinSystem))]
public sealed partial class MycotoxinInjectorComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> ContactedEntities = new();

    [DataField(required: true), AutoNetworkedField]
    public float MycotoxinPerSecond = 5f;

    [DataField, AutoNetworkedField]
    public TimeSpan TimeBetweenInjects = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan NextInjectionAt;

    [DataField, AutoNetworkedField]
    public bool AffectsDead;

    /// <summary>
    /// The prototype spawned once a victim's Mycotoxin exposure crosses InfectThreshold.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId EmbryoSpawn = "CMU14XenoBloodburster";
}
