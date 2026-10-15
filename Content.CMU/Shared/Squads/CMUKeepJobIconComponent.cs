using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Squads;

/// <summary>
/// On a squad leader of an auxiliary squad, keeps their job icon instead of showing the squad leader icon.
/// Networked because the squad itself is not always known to clients.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUKeepJobIconComponent : Component;
