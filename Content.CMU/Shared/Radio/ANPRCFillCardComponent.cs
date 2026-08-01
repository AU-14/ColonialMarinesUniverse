using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ANPRCFillCardComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Faction = string.Empty;

    [DataField, AutoNetworkedField]
    public string Designation = string.Empty;

    [DataField, AutoNetworkedField]
    public int Generation = 0;
}
