namespace Content.Shared._AU14.Callsigns;

/// <summary>
/// Added to a mob via its job's roundComponents to claim a fixed callsign
/// suffix (6 = leader, 5 = 2IC, 7 = senior NCO, ROMEO = RTO, OPS = staff)
/// </summary>
[RegisterComponent]
public sealed partial class AU14CallsignRoleComponent : Component
{
    [DataField(required: true)]
    public string Suffix = string.Empty;

    [DataField]
    public bool CommandElement;
}
