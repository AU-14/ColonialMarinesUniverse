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
    public bool IgnoreGuardianResistances = false;  
}
  
[Serializable, NetSerializable]  
public enum GuardianAbsorbType  
{  
    Generic,  
    AuraLink,  
    DirectLink,  
    PassiveCritSave,  
}
