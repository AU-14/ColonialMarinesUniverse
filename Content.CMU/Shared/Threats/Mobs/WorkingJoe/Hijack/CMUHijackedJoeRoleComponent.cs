using Content.Shared.Roles.Components;

namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;

/// <summary>
/// Marks the hijacked Working Joe mind role, so the cure and a promotion to Early Warning Joe remove exactly that
/// role and leave the player's job and other roles alone. Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHijackedJoeRoleComponent : BaseMindRoleComponent;
