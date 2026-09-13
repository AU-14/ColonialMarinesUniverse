using System.Numerics;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Dropship.Utility.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
public sealed partial class RMCEquipmentDeployerComponent : Component
{
    /// <summary>
    ///     The prototype to deploy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? DeployPrototype = "RMCML66DNestMetal";

    /// <summary>
    ///     The item slot to put the spawned entity in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string DeploySlotId = "dropship_deploy";

    /// <summary>
    ///     The text shown on the button in a dropship weapons control console.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string DropShipWindowButtonText = "MG";

    /// <summary>
    ///     The entity that will be deployed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? DeployEntity;

    /// <summary>
    ///     Whether the deployer can deploy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsDeployable;

    /// <summary>
    ///     Whether the stored entity is currently deployed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsDeployed;

    /// <summary>
    ///     Whether the deployer should automatically deploy when a dropship arrives at it's destination.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoDeploy;

    /// <summary>
    ///     Whether the dropship console should offer automatic deployment for this equipment.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanAutoDeploy = true;

    /// <summary>
    ///     Whether deployment must wait for server-side validation instead of being client-predicted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ServerAuthoritativeDeployment;

    /// <summary>
    ///     Whether the deployer should automatically undeploy when a dropship goes into FTL.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoUnDeploy;

    /// <summary>
    ///     Whether the deployer can be activated by interacting with it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsDeployableByHand;

    /// <summary>
    ///     The minimum alert level required to activate the deployer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public RMCAlertLevels AlertLevelRequired;

    /// <summary>
    ///     Blacklist for entities not allowed to use the deployer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    [DataField, AutoNetworkedField]
    public Vector2i StarboardForeDeployDirection = new(1, 0);

    [DataField, AutoNetworkedField]
    public Vector2i PortForeDeployDirection = new(-1, 0);

    [DataField, AutoNetworkedField]
    public Vector2i StarboardWingDeployDirection = new(0, -1);

    [DataField, AutoNetworkedField]
    public Vector2i PortWingDeployDirection = new(0, -1);

    [DataField, AutoNetworkedField]
    public float ForeDeployRotationDegrees = 180;

    [DataField, AutoNetworkedField]
    public float PortWingDeployRotationDegrees = -90;

    [DataField, AutoNetworkedField]
    public float StarboardWingDeployRotationDegrees = 90;

    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? UtilityDeployedSprite;

    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? WeaponDeployedSprite;

    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? ElectronicDeployedSprite;

    [DataField]
    public SoundSpecifier DeployAudio = new SoundPathSpecifier("/Audio/_RMC14/Machines/hydraulics_1.ogg");

    [DataField]
    public SoundSpecifier UnDeployAudio = new SoundPathSpecifier("/Audio/_RMC14/Machines/hydraulics_2.ogg");
}

[Serializable, NetSerializable]
public enum EquipmentDeployerVisuals
{
    Sprite,
    State,
}

[Serializable, NetSerializable]
public enum EquipmentDeployState
{
    UnDeployed,
    Deployed,
}

/// <summary>
/// Raised before equipment is moved out of or back into its deployer.
/// Specialized equipment can cancel deployment when its own placement requirements are not met.
/// </summary>
public sealed class RMCEquipmentDeployAttemptEvent(
    bool deploy,
    Vector2 deployOffset,
    EntityUid? user) : CancellableEntityEventArgs
{
    public readonly bool Deploy = deploy;
    public readonly Vector2 DeployOffset = deployOffset;
    public readonly EntityUid? User = user;
}

/// <summary>
/// Raised after equipment has been successfully moved out of or back into its deployer.
/// </summary>
public sealed class RMCEquipmentDeployedEvent(bool deployed, EntityUid equipment) : EntityEventArgs
{
    public readonly bool Deployed = deployed;
    public readonly EntityUid Equipment = equipment;
}
