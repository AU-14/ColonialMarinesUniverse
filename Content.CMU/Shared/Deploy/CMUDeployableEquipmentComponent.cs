using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Deploy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUDeployableEquipmentSystem))]
public sealed partial class CMUDeployableEquipmentComponent : Component
{
    /// <summary>
    ///     The current state of the deployable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CMUDeployedState DeployedState;

    /// <summary>
    ///     The duration of the deploying DoAfter.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DeployDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     The duration of the undeploying DoAfter.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UndeployDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     The skill that affects the deploy and undeploy duration.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent>? DelaySkill = "RMCSkillEngineer";

    /// <summary>
    ///     The tool quality needed to undeploy the deployable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ToolQualityPrototype> UndeployQuality = "Anchoring"; // RMC uses Pulsing; CMU deployables come down with a wrench, like sentries.

    /// <summary>
    ///     Whether the entity should be anchored when deployed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AnchorOnDeploy = true;

    /// <summary>
    ///     How far in front of the user the deployable should be placed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeployDistance = 1;

    /// <summary>
    ///     The name of the fixture that should be enabled/disabled when deploying/undeploying.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? DeployFixture = "deploy";
}

[Serializable, NetSerializable]
public enum CMUDeployedState
{
    Undeployed,
    Deployed,
}
