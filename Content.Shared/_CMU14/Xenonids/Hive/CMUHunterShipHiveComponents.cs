using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenonids.Hive;

/// <summary>
/// Identifies which of the two Hunter Ship xeno hives owns a map-placed specimen.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUHunterShipHiveAssignmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUHunterShipHiveKind Hive = CMUHunterShipHiveKind.Alpha;
}

/// <summary>
/// Runtime state for the Hunter Ship's two independent xeno hives.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class CMUHunterShipHiveBootstrapComponent : Component
{
    public EntityUid? AlphaHive;
    public EntityUid? ForsakenHive;
    public MapId? RootMap;
    public EntityUid? Network;
}

[Serializable, NetSerializable]
public enum CMUHunterShipHiveKind
{
    Alpha,
    Forsaken,
}
