using Content.Shared.Roles.Components;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marks the Early Warning Joe mind role so the rule can remove exactly that role when the antag
/// stands down (D10) without touching the player's job or other antag roles. It also keeps the Joe's
/// record for the round-end summary (CORE-04): it lives on the mind, so it outlasts a gibbed body, and it
/// remembers the highest level, which a respawn can lower. Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public bool Recorded;

    [DataField]
    public int HighestLevel;

    [DataField]
    public int ThrallsMade;

    [DataField]
    public int ConsolesAssimilated;
}
