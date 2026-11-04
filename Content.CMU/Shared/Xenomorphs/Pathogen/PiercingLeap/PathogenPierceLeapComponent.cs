using Content.Shared.Damage;  
using Content.Shared.FixedPoint;  
using Robust.Shared.Audio;  
using Robust.Shared.GameStates;  
using Robust.Shared.Prototypes;  
  
namespace Content.CMU.Shared.Xenomorphs.Pathogen.PierceLeap;  
  
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]  
public sealed partial class PathogenPierceLeapComponent : Component  
{
    [DataField, AutoNetworkedField]  
    public float Range = 6f;  
    
    [DataField, AutoNetworkedField]  
    public float Strength = 20f;  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan WindupTime = TimeSpan.FromSeconds(0.5);  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan Cooldown = TimeSpan.FromSeconds(12);  
    
    [DataField, AutoNetworkedField]  
    public float PathWidth = 1.0f;  
  
    [DataField, AutoNetworkedField]  
    public DamageSpecifier Damage = new();  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan StrikeDelay = TimeSpan.FromSeconds(0.75);  
  
    [DataField, AutoNetworkedField]  
    public int IgnoredCollisionGroupSmallm  = CollisionGroup.None;
  
    [DataField, AutoNetworkedField]  
    public int IgnoredCollisionGroupLarge = CollisionGroup.None;
  
    [DataField, AutoNetworkedField]  
    public SoundSpecifier? LeapSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/pathogen_pounce1.ogg");
  
    [DataField, AutoNetworkedField]  
    public SoundSpecifier? HitSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_flesh3.ogg");
  
    [DataField, AutoNetworkedField]  
    public EntProtoId? HitEffect = "RMCEffectTailHit";
  
    [DataField, AutoNetworkedField]  
    public EntProtoId? TagEffect = "CMEffectPunch";
}
