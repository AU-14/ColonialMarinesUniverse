using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapLinesComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<TacticalMapLine> MarineLines = new();

    [DataField, AutoNetworkedField]
    public List<TacticalMapLine> XenoLines = new();

    [DataField, AutoNetworkedField] // CMU14
    public List<TacticalMapLine> OpforLines = new();

    [DataField, AutoNetworkedField] // CMU14
    public List<TacticalMapLine> GovforLines = new();

    [DataField, AutoNetworkedField] // CMU14
    public List<TacticalMapLine> ClfLines = new();

    [DataField, AutoNetworkedField] // CMU14
    public List<TacticalMapLine> WeYuLines = new();
}
