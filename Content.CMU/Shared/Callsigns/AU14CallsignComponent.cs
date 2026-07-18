using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Callsigns;

// a faction member's assigned radio callsign ("ALPHA 6", "HAVOC ROMEO"), assigned
// automatically at spawn from job and squad
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14CallsignComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Faction = string.Empty;

    [DataField, AutoNetworkedField]
    public string Callsign = string.Empty;

    [DataField, AutoNetworkedField]
    public string Suffix = string.Empty;

    [DataField, AutoNetworkedField]
    public string JobTitle = string.Empty;

    [DataField]
    public EntityUid? Squad;

    [DataField]
    public bool RoleSuffix;

    // custom callsign group word set from the directory console, overrides the
    // squad/command element word while set
    [DataField]
    public string? Group;

    public GameTick RadioMaskTick;
}
