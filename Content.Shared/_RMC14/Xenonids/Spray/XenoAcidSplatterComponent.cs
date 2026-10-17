using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Spray;

// CMU14 Begin: explicit state handling safely resolves references that outlive their entities.
[RegisterComponent, NetworkedComponent]
[Access(typeof(XenoSprayAcidSystem))]
public sealed partial class XenoAcidSplatterComponent : Component
{
    [DataField]
    public EntityUid? Xeno;
}
// CMU14 End
