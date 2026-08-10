using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._CMU14.Xenomorphs.Pathogen.SporeSac;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeCloud;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUPathogenSporeCloudSystem), typeof(CMUPathogenSporeSacSystem))]
public sealed partial class CMUPathogenSporeCloudComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Inhaling;

    [DataField, AutoNetworkedField]
    public bool SilentInhale;

    [DataField, AutoNetworkedField]
    public TimeSpan DecayAt;

    [DataField, AutoNetworkedField]
    public EntProtoId EmbryoSpawn = "CMU14XenoBloodburster";
}