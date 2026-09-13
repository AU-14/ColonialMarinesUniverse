using Content.Shared.Roles.Components;

namespace Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;

/// <summary>
/// Marks a mind role granted by a synthetic subversion key, so reset does not
/// remove the patient's unrelated antagonist or job roles.
/// </summary>
[RegisterComponent]
public sealed partial class SubvertedSynthRoleComponent : BaseMindRoleComponent;
