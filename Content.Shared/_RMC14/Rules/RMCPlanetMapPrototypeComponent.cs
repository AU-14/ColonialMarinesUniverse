using Content.Shared._RMC14.Item;
using Content.Shared.AU14;
using Content.Shared.AU14.Allegiance;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Rules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCPlanetSystem), Other = AccessPermissions.ReadWriteExecute)]
public sealed partial class RMCPlanetMapPrototypeComponent : Component
{
    [DataField, AutoNetworkedField]
    public string MapId = string.Empty;

    [DataField(required: true), AutoNetworkedField, Access(Other = AccessPermissions.ReadExecute)]
    public ResPath Map;

    [DataField, AutoNetworkedField]
    public CamouflageType Camouflage = CamouflageType.Jungle;

    [DataField, AutoNetworkedField]
    public int MinPlayers;

    [DataField, AutoNetworkedField]
    public int MaxPlayers;

    [DataField, AutoNetworkedField]
    public string? Announcement;

    [DataField, AutoNetworkedField]
    public List<(ProtoId<JobPrototype> Job, int Amount)>? SurvivorJobs;

    /// <summary>
    /// Will override a preferred job to a list of other variants
    /// For example, if you have security survivor selected it will pick one of the variants
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<JobPrototype>, List<(ProtoId<JobPrototype> Variant, int Amount)>>? SurvivorJobVariants;

    [DataField("joboverrides"), AutoNetworkedField]
    public Dictionary<ProtoId<JobPrototype>, ProtoId<JobPrototype>>? ColonyJobOverrides;

    /// <summary>
    /// Will override a preferred job to another
    /// Useful for FORECON so any survivor preference will be overriden to FORECON survivor
    /// Basically, if security survivor is overriden by forecon survivor, it will be as if sec survivor: high is forecon survivor: high
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<JobPrototype>, ProtoId<JobPrototype>>? SurvivorJobOverrides;

    /// <summary>
    /// Instead of using the limits of the variant, this will select a random variant and use the base job's limit when true.
    /// If it is false, it will use the slots of the variant instead of a random one. See Chance's Claim.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SelectRandomSurvivorVariant = true;

    /// <summary>
    /// List of base survivor jobs that appear in a specific scenario. These have a higher priority than other job types.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, List<(ProtoId<JobPrototype> Job, int Amount)>>? SurvivorJobScenarios;

    /// <summary>
    /// List of survivor job variant that appear in a specific scenario. These have a higher priority than other job types.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, Dictionary<ProtoId<JobPrototype>, List<(ProtoId<JobPrototype> Special, int Amount)>>>? SurvivorJobVariantScenarios;

    /// <summary>
    /// List of survivor job overrides that are in a specific scenario.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, Dictionary<ProtoId<JobPrototype>, ProtoId<JobPrototype>>>? SurvivorJobOverrideScenarios;

    /// <summary>
    /// List of nightmare scenarios that can occur, which are used for conditionally spawning map inserts.
    /// Only one scenario will be selected using cumulative probability.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<RMCNightmareScenario>? NightmareScenarios;

    [DataField, AutoNetworkedField]
    public bool InRotation = true;

    /// <summary>
    /// Special faxes that should be sent roundstart.
    /// The dictionary is the fax ID and then the entity to be faxed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, EntProtoId<PaperComponent>>? SpecialFaxes;

    [DataField, AutoNetworkedField]
    public List<ProtoId<PlatoonPrototype>> PlatoonsGovfor = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<PlatoonPrototype>> PlatoonsOpfor = new();

    [DataField("defaultgovfor")]
    public string? DefaultGovforPlatoon;

    [DataField("defaultopfor")]
    public string? DefaultOpforPlatoon;

    [DataField("daycycleenabled"), AutoNetworkedField]
    public bool DaycycleEnabled = true;

    [DataField("govforinship"), AutoNetworkedField]
    public bool GovforInShip;

    [DataField("opforinship"), AutoNetworkedField]
    public bool OpforInShip;

    [DataField("votename")]
    public string? VoteName = string.Empty;

    [DataField("lorePrimer")]
    public ProtoId<LorePrimerPrototype>? LorePrimer;

    [DataField("faction")]
    public string? Faction = string.Empty;

    [DataField("Allegiance"), AutoNetworkedField]
    public ProtoId<AllegiancePrototype>? Allegiance;

    [DataField("govforfighters")]
    public int govforfighters;

    [DataField("opforfighters")]
    public int opforfighters;

    [DataField("govfordropships")]
    public int govfordropships = 2;

    [DataField("opfordropships")]
    public int opfordropships = 2;

    [DataField("threats")]
    public List<ProtoId<ThreatPrototype>> AllowedThreats = new();

    [DataField("thirdparties")]
    public List<ProtoId<ThirdPartyPrototype>> ThirdParties = new();

    [DataField("thirdpartyinterval"), AutoNetworkedField]
    public int? ThirdPartyInterval = 14000;

    [DataField("jobScalingFof")]
    public ProtoId<JobScalePrototype>? JobScalingFof;

    [DataField("jobScalingIns")]
    public ProtoId<JobScalePrototype>? JobScalingIns;
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial record RMCNightmareScenario
{
    [DataField(required: true)]
    public string ScenarioName = string.Empty;

    [DataField]
    public float ScenarioProbability = 1.0f;
}
