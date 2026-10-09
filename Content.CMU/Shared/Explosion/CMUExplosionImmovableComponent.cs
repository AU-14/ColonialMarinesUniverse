using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Explosion;

// for draggable machines a faction can't lose: no Damageable keeps them alive, but a blast still
// flings a Dynamic body off the grid or down a deck and nobody can find it again
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUExplosionImmovableComponent : Component;
