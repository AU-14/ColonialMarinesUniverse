using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.TacticalMap;

/// <summary>
///     Bucket override like <see cref="WeYuMapTrackedComponent" />: pair with
///     TacticalMapTracked (which drives the update lifecycle) so the entity
///     blips in the abomination-only channel instead of falling through to the
///     marine map. Invisible to every other faction.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class AbominationMapTrackedComponent : Component;
