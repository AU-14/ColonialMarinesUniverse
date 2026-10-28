using Robust.Shared.GameStates;  
using Content.Shared.FixedPoint;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianLink;  
  
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]  
[Access(typeof(GuardianLinkSystem))]  
public sealed partial class XenoGuardianComponent : Component  
{  
    [DataField, AutoNetworkedField]  
    public float Range = 10;  
  
    [DataField, AutoNetworkedField]  
    public FixedPoint2 LinkCost = FixedPoint2.New(50);  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan LinkDuration = TimeSpan.FromSeconds(15);  
  
    [DataField, AutoNetworkedField]  
    public float AbsorbPercent = 0.5f;  
}
