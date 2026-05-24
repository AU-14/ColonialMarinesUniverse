using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Marker — entity can be infected and assimilated by abominations even
/// though it isn't a humanoid. Tagging an animal mob (rat, monkey, cow…)
/// with this lets mimics melee-infect or assimilate it; the resulting
/// profile keys off the entity prototype id so all rats group as "rat",
/// all monkeys as "monkey", etc.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AbominationInfectableComponent : Component;
