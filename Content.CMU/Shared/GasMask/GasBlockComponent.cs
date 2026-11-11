using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.GasMask;

// cmss13's BLOCKGASEFFECT: worn on the mask, eyes or head it keeps chem smoke and nerve gas out entirely,
// no tank or filter needed. a pulled-down mask doesn't count
[RegisterComponent, NetworkedComponent]
public sealed partial class GasBlockComponent : Component;
