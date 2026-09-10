using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Power;

// CMU14 AreaPowerState Begin: serialize retained live members through the owning power system.
// [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedRMCPowerSystem))]
public sealed partial class RMCAreaPowerComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Apcs = new();

    [DataField]
    public HashSet<EntityUid> EquipmentReceivers = new();

    [DataField]
    public HashSet<EntityUid> LightingReceivers = new();

    [DataField]
    public HashSet<EntityUid> EnvironmentReceivers = new();

    [DataField]
    public int[] Load = new int[Enum.GetValues<RMCPowerChannel>().Length];
}
// CMU14 End
