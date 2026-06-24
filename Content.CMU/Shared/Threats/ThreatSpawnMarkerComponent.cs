using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Threats;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ThreatSpawnMarkerComponent : Component
{

    [DataField("ID", required: false)]
    public string ID { get; private set; } = "";

    [DataField("threatmarkertype", required: false)]
    public ThreatMarkerType ThreatMarkerType { get; private set; } = ThreatMarkerType.Member;

    [DataField("thirdparty", required: false)]
    public bool ThirdParty { get; private set; } = false;

    // Cooldown before marker can be reused (in seconds)
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [AutoNetworkedField]
    public TimeSpan NextAvailableAt = TimeSpan.Zero;
}

public enum ThreatMarkerType
{
    Leader,
    Entity,
    Member,
}
