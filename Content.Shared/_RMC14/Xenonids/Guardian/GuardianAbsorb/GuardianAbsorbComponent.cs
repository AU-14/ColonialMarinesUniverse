using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;
  
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]  
public sealed partial class GuardianAbsorbComponent : Component  
{  

    [DataField, AutoNetworkedField]  
    public EntityUid? Guardian;  
  
 
    [DataField, AutoNetworkedField]  
    public GuardianAbsorbType Type = GuardianAbsorbType.Generic;  
  

    [DataField, AutoNetworkedField]  
    public float AbsorbPercent = 0.5f;  
  

    [DataField, AutoNetworkedField]  
    public bool IgnoreGuardianResistances = true;  
}  

[Serializable, NetSerializable]
public enum GuardianAbsorbType  
{  
    Generic,
    AuraLink,
    DirectLink,
    PassiveCritSave,
}
