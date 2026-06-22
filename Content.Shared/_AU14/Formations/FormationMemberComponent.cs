using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Formations;

/// <summary>
/// Marks an entity as an active formation member (leader or follower).
/// Exists on both client and server so the shared mob-collision system
/// can cancel push events, making formation members fully ghost-like
/// to all other players during movement.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FormationMemberComponent : Component { }
