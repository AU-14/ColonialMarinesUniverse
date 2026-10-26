namespace Content.Shared._RMC14.Xenonids.GuardianSweep;  
  
[RegisterComponent, NetworkedComponent]  
public sealed partial class XenoGuardianSweepComponent : Component  
{  
    [DataField]  
    public DamageSpecifier Damage = new();  
  
    [DataField]  
    public float Range = 1.5f;  
  
    [DataField]  
    public EntProtoId Effect = "RMCEffectExtraSlash";  
  
    [DataField]  
    public SoundSpecifier Sound = new SoundCollectionSpecifier("AlienClaw");  
  
    [DataField]  
    public ProtoId<EmotePrototype> HissEmote = "Hiss";  
  
    [DataField]  
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(2);  
  
    [DataField]  
    public float KnockBackDistance = 1f;  
}
