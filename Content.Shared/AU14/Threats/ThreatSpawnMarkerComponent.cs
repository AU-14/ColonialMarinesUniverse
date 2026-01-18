using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Threats;
[RegisterComponent]

public sealed partial class ThreatSpawnMarkerComponent: Component


{

    [DataField("ID", required: false)]
    public string ID  { get; private set; } = "";

    // if unchanged is considered genericc


    [DataField("threatmarkertype", required: false)]
    public ThreatMarkerType ThreatMarkerType  { get; private set; } = ThreatMarkerType.Member;

    [DataField("thirdparty", required: false)]
    public bool ThirdParty { get; private set; } = false;

}

public enum  ThreatMarkerType
{
    Leader,
    Entity,
    Member,
}
