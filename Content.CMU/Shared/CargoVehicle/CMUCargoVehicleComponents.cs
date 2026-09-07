using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CargoVehicle;

[Serializable, NetSerializable]
public enum CMUCargoVehicleArmingMode : byte
{
    None,
    Manual,
    Automatic,
}

[Serializable, NetSerializable]
public enum CMUCargoVehicleVisuals : byte
{
    BayOpen,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCargoVehicleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool BayOpen;

    [DataField, AutoNetworkedField]
    public EntityUid? Controller;

    [DataField, AutoNetworkedField]
    public CMUCargoVehicleArmingMode ArmingMode;

    [DataField]
    public string CargoContainerId = "cmu-cargo-crate";

    [DataField]
    public FixedPoint2 AutomaticArmDamage = FixedPoint2.New(300);

    [DataField]
    public FixedPoint2 MaximumDamage = FixedPoint2.New(400);

    [DataField]
    public TimeSpan CargoDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float DetonationDelay = 5f;

    [DataField]
    public float BeepInterval = 1f;

    [DataField]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/_RMC14/Medical/reset_key_shortbeep.ogg");

    [DataField]
    public SoundSpecifier RampSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/hydraulics_1.ogg");

    [DataField]
    public EntProtoId WreckPrototype = "CMUCargoCarrierWreck";

    [DataField]
    public List<EntProtoId> DebrisPrototypes = new()
    {
        "CMUCargoCarrierDebris",
        "CMUCargoCarrierDebrisSide",
        "CMUCargoCarrierDebrisHood",
        "CMUCargoCarrierDebrisBumper",
    };

    [DataField]
    public EntProtoId OilSpawnerPrototype = "RMCDecalSpawnerOilSplatters";

    [DataField]
    public EntProtoId FirePrototype = "RMCTileFire";

    [DataField]
    public int FireRange = 1;

    [DataField]
    public int FireDuration = 20;

    [DataField]
    public int MinimumDebris = 4;

    [DataField]
    public int MaximumDebris = 7;

    public ContainerSlot? CargoContainer;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCargoVehicleControllerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedVehicle;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCargoVehicleRemotePilotComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    [DataField, AutoNetworkedField]
    public EntityUid Controller;

    [DataField, AutoNetworkedField]
    public EntityUid MindId;

    public bool HadSsdIndicator;

    public ProtoId<SsdIconPrototype> SsdIndicatorIcon = "SSDIcon";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCargoVehicleControlSessionComponent : Component
{
    public bool Ending;

    [DataField, AutoNetworkedField]
    public EntityUid Operator;

    [DataField, AutoNetworkedField]
    public EntityUid Controller;

    [DataField, AutoNetworkedField]
    public EntityUid MindId;

    [DataField, AutoNetworkedField]
    public EntityUid? ReturnAction;

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleBayAction;

    [DataField, AutoNetworkedField]
    public EntityUid? SelfDestructAction;
}
