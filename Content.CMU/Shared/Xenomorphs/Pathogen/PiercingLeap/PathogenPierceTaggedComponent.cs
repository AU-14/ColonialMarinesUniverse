using Content.Shared.Damage;  
using Robust.Shared.Audio;  
using Robust.Shared.GameStates;  
using Robust.Shared.Prototypes;  
  
namespace Content.CMU.Shared.Xenomorphs.Pathogen.PierceLeap;  
 
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]  
public sealed partial class PathogenPierceTaggedComponent : Component  
{  
    [DataField, AutoNetworkedField]  
    public DamageSpecifier Damage = new();  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan? StrikeAt;  
  
    [DataField, AutoNetworkedField]  
    public EntityUid? Origin;  
  
    [DataField, AutoNetworkedField]  
    public EntProtoId? HitEffect;  
  
    [DataField, AutoNetworkedField]  
    public SoundSpecifier? HitSound;  
}
