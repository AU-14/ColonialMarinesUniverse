using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Callsigns;

/// <summary>
/// A faction member's assigned radio callsign, e.g. "ALPHA 6" or "HAVOC ROMEO".
/// Assigned automatically at spawn from job and squad
/// </summary>
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

    public GameTick RadioMaskTick;
}
