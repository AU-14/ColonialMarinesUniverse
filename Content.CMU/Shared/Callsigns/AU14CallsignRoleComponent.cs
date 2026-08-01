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

    // directory console section this role is listed under (AIR, MP, MEDICAL, INTEL);
    // null = command element or squad as usual
    [DataField]
    public string? Category;

    // diplomats and similar roles get no callsign and never appear on the net directory
    [DataField]
    public bool Exempt;
}
