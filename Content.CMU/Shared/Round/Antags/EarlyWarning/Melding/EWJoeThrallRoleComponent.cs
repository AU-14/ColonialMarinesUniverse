using Content.Shared.Roles.Components;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// Marks the River-Melded thrall mind role so the cure removes exactly that role and nothing else.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeThrallRoleComponent : BaseMindRoleComponent;
