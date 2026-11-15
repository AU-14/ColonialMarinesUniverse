using Content.Shared.FixedPoint;  
using Robust.Shared.GameStates;  
using Robust.Shared.Serialization;  
  
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
    public bool IgnoreGuardianResistances = false;  
  
    [DataField, AutoNetworkedField]  
    public FixedPoint2 GuardianCachedDamage;  
}  
  
[Serializable, NetSerializable]  
public enum GuardianAbsorbType  
{  
    Generic,  
    AuraLink,  
    DirectLink,  
    PassiveCritSave,  
}
