using Content.Shared.DoAfter;  
using Robust.Shared.Map;  
using Robust.Shared.Serialization;  
  
namespace Content.CMU.Shared.Xenomorphs.Pathogen.PierceLeap;  
  
[Serializable, NetSerializable]  
public sealed partial class PathogenPierceLeapDoAfterEvent : SimpleDoAfterEvent  
{  
    [DataField]  
    public NetCoordinates Coordinates;  
  
    public PathogenPierceLeapDoAfterEvent(NetCoordinates coordinates)  
    {  
        Coordinates = coordinates;  
    }  
}
