using Content.Shared.Damage;  
using Robust.Shared.Audio;  
using Robust.Shared.GameStates;  
using Robust.Shared.Map;  
using Robust.Shared.Prototypes;  
  
namespace Content.CMU.Shared.Xenomorphs.Pathogen.PierceLeap;  

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]  
public sealed partial class PathogenPierceLeapingComponent : Component  
{  
    [DataField, AutoNetworkedField]  
    public EntityCoordinates Origin;  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan LeapEndTime;  
  
    [DataField, AutoNetworkedField]  
    public HashSet<EntityUid> Tagged = new();  
  
    [DataField, AutoNetworkedField]  
    public DamageSpecifier Damage = new();  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan StrikeDelay;  
  
    [DataField, AutoNetworkedField]  
    public float PathWidth;  
  
    [DataField, AutoNetworkedField]  
    public EntProtoId? HitEffect;  
  
    [DataField, AutoNetworkedField]  
    public SoundSpecifier? HitSound;  
}
