using Robust.Shared.GameStates;  
using Content.Shared.FixedPoint;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
  
[RegisterComponent, NetworkedComponent]  
public sealed partial class GuardianAbsorbTrackedComponent : Component  
{  
    public FixedPoint2 GuardianCachedDamage;  
}
