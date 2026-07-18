namespace Content.Shared._AU14.Callsigns;

// added via the job's roundComponents to claim a fixed callsign suffix
// (6 = leader, 5 = 2IC, 7 = senior NCO, ROMEO = RTO, OPS = staff)
[RegisterComponent]
public sealed partial class AU14CallsignRoleComponent : Component
{
    // empty = keep the automatic numbered suffix, only the tag/element applies
    [DataField]
    public string Suffix = string.Empty;

    [DataField]
    public bool CommandElement;

    // short role tag shown before the callsign on radio, e.g. "SL" -> (SL) ALPHA 6
    [DataField]
    public string? RadioTag;
}
