using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Marks an abomination-fired projectile. Carries no state and exists so
///     <see cref="AbominationCombatSystem" /> can cancel friendly-fire hits
///     against fellow abominations and disguised mimics, the way
///     XenoProjectileComponent does for hive-mates.
/// </summary>
[RegisterComponent]
public sealed partial class AbominationProjectileComponent : Component;
