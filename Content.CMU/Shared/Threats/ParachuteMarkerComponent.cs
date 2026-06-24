using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Threats;

[RegisterComponent, NetworkedComponent]
public sealed partial class ParachuteMarkerComponent : Component
{
    [DataField("id", required: false)]
    public string ID { get; private set; } = string.Empty;

}
