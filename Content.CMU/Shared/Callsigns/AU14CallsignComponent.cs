using Robust.Shared.Timing;

namespace Content.Shared._AU14.Callsigns;

// a faction member's assigned radio callsign ("ALPHA 6", "HAVOC ROMEO"), assigned
// automatically at spawn from job and squad.
//
// deliberately NOT networked: a networked callsign ships every faction member's
// callsign, suffix and job title to every client that has them in PVS, which hands
// the enemy roster to anyone reading their own client's state. the directory console
// is the only way this data reaches a player, and it gates on faction
[RegisterComponent]
public sealed partial class AU14CallsignComponent : Component
{
    [DataField]
    public string Faction = string.Empty;

    [DataField]
    public string Callsign = string.Empty;

    [DataField]
    public string Suffix = string.Empty;

    [DataField]
    public string JobTitle = string.Empty;

    [DataField]
    public EntityUid? Squad;

    [DataField]
    public bool RoleSuffix;

    // custom callsign group word set from the directory console, overrides the
    // squad/command element word while set
    [DataField]
    public string? Group;

    // directory console section, copied from the role at assignment
    [DataField]
    public string? Category;

    public GameTick RadioMaskTick;
}
