using Content.Shared.FixedPoint;  
using Robust.Shared.GameStates;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianCritSave;  
  
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]  
[Access(typeof(GuardianCritSaveSystem))]  
public sealed partial class GuardianCritSaveComponent : Component  
{  
    [DataField, AutoNetworkedField]  
    public float Range = 8f;  
  
    [DataField, AutoNetworkedField]  
    public float AbsorbPercent = 1f;  
  
    [DataField, AutoNetworkedField]  
    public FixedPoint2 MinimumHealth = FixedPoint2.New(200);  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan CheckEvery = TimeSpan.FromSeconds(1);  
  
    [DataField, AutoNetworkedField]  
    public TimeSpan NextCheck;  
  
    [DataField, AutoNetworkedField]  
    public HashSet<EntityUid> ActiveTargets = new();  
}
