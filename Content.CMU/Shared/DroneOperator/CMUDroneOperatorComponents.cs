using Content.Shared.Dataset;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.StatusIcon;
using Content.Shared.Tools;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.DroneOperator;

[Serializable]
public enum CMUDroneAssemblyPartSlot : byte
{
    Head,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
}

public enum CMUDroneAssemblyPartState : byte
{
    Missing,
    Installed,
    Clamped,
    Welded,
}

[RegisterComponent]
public sealed partial class CMUDroneOperatorComponent : Component
{
    [DataField]
    public EntProtoId FollowActionId = "CMUActionDroneFollow";

    [DataField]
    public EntProtoId StopFollowActionId = "CMUActionDroneStopFollow";

    [DataField]
    public EntProtoId TransferEffectId = "CMUDroneOperatorTransferEffect";

    [DataField]
    public EntProtoId ConnectBeamEffectId = "CMUDroneOperatorConnectBeamEffect";

    [DataField]
    public EntityUid? Drone;

    [DataField]
    public EntityUid? Tablet;

    [DataField]
    public EntityUid? ControlledDrone;

    public EntityUid? FollowAction;

    public EntityUid? StopFollowAction;

    public EntityUid? TransferEffect;
}

[RegisterComponent]
public sealed partial class CMUDroneControlTabletComponent : Component
{
    [DataField]
    public EntityUid? LinkedDrone;

    [DataField]
    public EntityUid? Operator;

    [DataField]
    public float Range = 30f;

    [DataField]
    public float RangeWarningBuffer = 5f;

    [DataField]
    public TimeSpan RangeWarningInterval = TimeSpan.FromSeconds(2);
}

[RegisterComponent]
public sealed partial class CMUDroneAndroidComponent : Component
{
    [DataField]
    public string ModuleContainerId = "cmu-drone-module";

    [DataField]
    public EntityUid? Operator;

    [DataField]
    public EntityUid? Tablet;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> NameDataset = "CMUNamesDroneAndroid";

    [DataField]
    public int MaxNameLength = 24;

    [DataField]
    public EntProtoId RuinedCorePrototype = "CMURuinedDroneCore";

    [DataField]
    public EntProtoId DormantEffectId = "CMUDroneAndroidDormantEffect";

    [DataField]
    public EntProtoId DisconnectBeamEffectId = "CMUDroneAndroidDisconnectBeamEffect";

    [DataField]
    public TimeSpan TransferShakeDuration = TimeSpan.FromSeconds(0.1);

    [DataField]
    public Color DormantEyeColor = Color.FromHex("#ff0000");

    [DataField]
    public float FollowCloseRange = 1f;

    [DataField]
    public float FollowRange = 1.5f;

    public ContainerSlot? ModuleContainer;

    public EntityUid? InstalledModule;

    public bool RuinedCoreSpawned;

    public bool FollowingOperator;

    public EntityUid? DormantEffect;
}

[RegisterComponent]
public sealed partial class CMUDroneFrameComponent : Component
{
    [DataField(required: true)]
    public EntProtoId DronePrototype;

    [DataField]
    public string PartsContainerId = "cmu-drone-frame-parts";

    [DataField]
    public List<CMUDroneAssemblyPartSlot> RequiredParts = new()
    {
        CMUDroneAssemblyPartSlot.Head,
        CMUDroneAssemblyPartSlot.LeftArm,
        CMUDroneAssemblyPartSlot.RightArm,
        CMUDroneAssemblyPartSlot.LeftLeg,
        CMUDroneAssemblyPartSlot.RightLeg,
    };

    [DataField]
    public ProtoId<ToolQualityPrototype> OpenTool = "Screwing";

    [DataField]
    public ProtoId<ToolQualityPrototype> ClampTool = "Anchoring";

    [DataField]
    public ProtoId<ToolQualityPrototype> WeldTool = "Welding";

    [DataField]
    public TimeSpan OpenDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan InstallDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan ClampDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan WeldDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ActivateDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float WeldFuel = 5f;

    public bool PortsOpen;

    public Dictionary<CMUDroneAssemblyPartSlot, CMUDroneAssemblyPartState> PartStates = new();

    public Dictionary<CMUDroneAssemblyPartSlot, EntityUid> InstalledParts = new();

    public Container? PartsContainer;
}

[RegisterComponent]
public sealed partial class CMUDroneAssemblyPartComponent : Component
{
    [DataField(required: true)]
    public CMUDroneAssemblyPartSlot Part;
}

[RegisterComponent]
public sealed partial class CMUDroneSynthKeyComponent : Component
{
}

[RegisterComponent]
public sealed partial class CMUDroneModuleComponent : Component
{
    [DataField]
    public ProtoId<ToolQualityPrototype> InstallTool = "Screwing";

    [DataField]
    public TimeSpan InstallDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();
}

[RegisterComponent]
public sealed partial class CMURuinedDroneCoreComponent : Component
{
}

[RegisterComponent]
public sealed partial class CMURemotePilotingComponent : Component
{
    public EntityUid Drone;
    public EntityUid Tablet;
    public EntityUid MindId;
    public bool BlocksInput = true;
    public TimeSpan BodyMoveGraceUntil;
    public bool HadSsdIndicator;
    public ProtoId<SsdIconPrototype> SsdIndicatorIcon = "SSDIcon";
}

[RegisterComponent]
public sealed partial class CMUDroneControlSessionComponent : Component
{
    public EntityUid Operator;
    public EntityUid Tablet;
    public EntityUid MindId;
    public EntityUid? EndControlAction;
    public TimeSpan NextLeashWarning;
    public bool SkillsSnapshotTaken;
    public bool HadSkills;
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int>? PreviousSkills;
}
