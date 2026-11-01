using Content.Shared.Roles.Components;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marks the Early Warning Joe mind role so the rule can remove exactly that role when the antag
/// stands down (D10) without touching the player's job or other antag roles.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeRoleComponent : BaseMindRoleComponent;
