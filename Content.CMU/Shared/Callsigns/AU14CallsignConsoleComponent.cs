using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Callsigns;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14CallsignConsoleComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string Faction = string.Empty;
}
