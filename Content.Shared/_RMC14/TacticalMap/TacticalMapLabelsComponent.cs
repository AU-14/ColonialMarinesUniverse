using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapLabelsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, string> MarineLabels = new();

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, string> XenoLabels = new();

    [DataField, AutoNetworkedField] // CMU14
    public Dictionary<Vector2i, string> OpforLabels = new();

    [DataField, AutoNetworkedField] // CMU14
    public Dictionary<Vector2i, string> GovforLabels = new();

    [DataField, AutoNetworkedField] // CMU14
    public Dictionary<Vector2i, string> ClfLabels = new();

    [DataField, AutoNetworkedField] // CMU14
    public Dictionary<Vector2i, string> WeYuLabels = new();
}
