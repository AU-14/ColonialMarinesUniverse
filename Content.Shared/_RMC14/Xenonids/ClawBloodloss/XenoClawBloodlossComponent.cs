using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.ClawBloodloss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ClawXenoBloodlossSystem))]
public sealed partial class XenoClawBloodlossComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Bloodloss = FixedPoint2.Zero;
}
