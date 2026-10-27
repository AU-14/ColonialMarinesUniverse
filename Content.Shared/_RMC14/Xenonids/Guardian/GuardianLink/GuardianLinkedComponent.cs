using Robust.Shared.GameStates;  
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianLink;  
  
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]  
[Access(typeof(GuardianLinkSystem))]  
public sealed partial class GuardianLinkedComponent : Component  
{  
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]  
    public TimeSpan ExpiresAt;  
}
