using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Pulling;

// cmss13 /datum/pain/yautja: a hostile xeno dragging you in crit doesn't choke you out. yautja have it, and the
// Thrall species got the yautja pain datum for exactly this in PR 11542
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUNoDragSuffocationComponent : Component;
