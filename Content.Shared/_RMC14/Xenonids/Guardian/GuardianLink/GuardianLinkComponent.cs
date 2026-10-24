using Content.Shared.Actions;  
using Content.Shared.FixedPoint;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianLink;
  
public sealed partial class XenoGuardianLinkActionEvent : EntityTargetActionEvent  
{  
    [DataField]  
    public float AbsorbPercent = 0.5f;  
  
    [DataField]  
    public TimeSpan Duration = TimeSpan.FromSeconds(15);  
}
