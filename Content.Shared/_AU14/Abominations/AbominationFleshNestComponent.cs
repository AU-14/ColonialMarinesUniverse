using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Marker on every flesh nest. Spawning is driven globally by
/// AbominationNestSpawnSystem on the server — each placed nest contributes
/// to the global spawn rate, but only one nest spawns per tick.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AbominationFleshNestComponent : Component;
