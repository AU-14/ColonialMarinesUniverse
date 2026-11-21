using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.Sporecaster;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPathogenSporecasterComponent : Component
{
    [DataField, AutoNetworkedField]
    public int StoredClouds = 0;

    [DataField, AutoNetworkedField]
    public int MaxClouds = 6;

    [DataField, AutoNetworkedField]
    public TimeSpan GrowInterval = TimeSpan.FromSeconds(120);

    [DataField, AutoNetworkedField]
    public TimeSpan NextGrowAt;

    [DataField, AutoNetworkedField]
    public float DetectionRange = 2f;

    [DataField, AutoNetworkedField]
    public float DestructionReleaseChance = 0.6f;

    [DataField, AutoNetworkedField]
    public EntProtoId SporeCloudProto = "CMU14PathogenSporeCloud";

    [DataField, AutoNetworkedField]
    public EntProtoId ParasiteProto = "CMU14XenoPopper";

    [DataField, AutoNetworkedField]
    public TimeSpan AutoReleaseInterval = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan NextAutoReleaseAt;

    /// <summary>
    /// Roll per auto-release tick to tear a spore-protective mask off a nearby protected target.
    /// 0.05f Equates to roughly 60 seconds exposure.
    /// </summary>
    [DataField]
    public float MaskStripChance = 0.05f;

    [DataField, AutoNetworkedField]
    public List<EntityUid> LivePoppers = new();
}
