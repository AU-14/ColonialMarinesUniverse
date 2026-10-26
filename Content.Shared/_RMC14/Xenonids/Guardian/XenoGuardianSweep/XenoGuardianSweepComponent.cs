using Content.Shared.Chat.Prototypes;  
using Content.Shared.Damage;  
using Robust.Shared.Audio;  
using Robust.Shared.GameStates;  
using Robust.Shared.Prototypes; 

namespace Content.Shared._RMC14.Xenonids.GuardianSweep;  
  
[RegisterComponent, NetworkedComponent]  
public sealed partial class XenoGuardianSweepComponent : Component  
{  
    [DataField]  
    public DamageSpecifier Damage = new();  
  
    [DataField]  
    public float Range = 1.5f;  
  
    [DataField]  
    public EntProtoId Effect = "CMEffectPunch";  
  
    [DataField]  
    public SoundSpecifier Sound = new SoundCollectionSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");  
  
    [DataField]  
    public ProtoId<EmotePrototype> HissEmote = "Hiss";  
  
    [DataField]  
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(5);  
  
    [DataField]  
    public float KnockBackDistance = 2f;  
}
