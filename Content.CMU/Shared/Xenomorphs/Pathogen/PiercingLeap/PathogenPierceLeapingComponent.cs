using Content.Shared.Damage;
using System.Numerics;  
using Robust.Shared.Physics;
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
    public MapCoordinates Destination;  
  
    [DataField, AutoNetworkedField]  
    public Vector2 Direction;  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan LeapEndTime;  
  
    [DataField, AutoNetworkedField]  
    public DamageSpecifier Damage = new();  
  
    [DataField, AutoNetworkedField]  
    public float PathWidth = 1.0f;  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan StrikeDelay = TimeSpan.FromSeconds(0.75);  
  
    [DataField, AutoNetworkedField]  
    public EntProtoId? HitEffect;  
  
    [DataField, AutoNetworkedField]  
    public SoundSpecifier? HitSound;  
  
    [DataField, AutoNetworkedField]  
    public EntProtoId? TagEffect;  
  
    [DataField, AutoNetworkedField]  
    public CollisionGroup IgnoredCollisionGroupLarge = CollisionGroup.None;  
  
    [DataField, AutoNetworkedField]  
    public CollisionGroup IgnoredCollisionGroupSmall = CollisionGroup.None;  
  
    public HashSet<EntityUid> Tagged = new();  
}
