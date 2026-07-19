using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Callsigns;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14CallsignConsoleComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string Faction = string.Empty;

    // a viewing terminal: shows the roster but refuses every edit, regardless
    // of the viewer's training (e.g. the directory carried on ANPRC packs)
    [DataField, AutoNetworkedField]
    public bool ReadOnly;
}
