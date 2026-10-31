using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Core;

// cmss13 NO_PERMANENT_DAMAGE: no bleeding at all, no broken bones and no limbs lost while alive.
// the hero human species has it, which makes it the thrall's too
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUNoPermanentDamageComponent : Component;
