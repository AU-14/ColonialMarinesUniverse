using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Blackfoot;

[Serializable, NetSerializable]
public enum BlackfootFlightState : byte
{
    Stowed,
    Grounded,
    Idling,
    TakingOff,
    VTOL,
    Flight,
    Landing,
    Crashed,
}

[Serializable, NetSerializable]
public enum BlackfootMovementMode : byte
{
    VTOL,
    Flight,
}

[Serializable, NetSerializable]
public enum BlackfootLandingPadState : byte
{
    Folded,
    Deploying,
    Deployed,
}

[Serializable, NetSerializable]
public enum BlackfootLandingPadLightState : byte
{
    Off,
    Ready,
    Servicing,
}

[Serializable, NetSerializable]
public enum BlackfootSupportPackStage : byte
{
    Secured,
    AnchorsLoosened,
    PanelOpen,
}

public enum BlackfootLandingPadAttachment : byte
{
    None,
    FuelPump,
    FlightComputer,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootFlightComponent : Component
{
    [DataField, AutoNetworkedField]
    public BlackfootFlightState State = BlackfootFlightState.Grounded;

    [DataField, AutoNetworkedField]
    public BlackfootFlightState PreviousState = BlackfootFlightState.Grounded;

    [DataField, AutoNetworkedField]
    public BlackfootMovementMode MovementMode = BlackfootMovementMode.VTOL;

    [DataField, AutoNetworkedField]
    public TimeSpan StateStartedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan TakeoffDuration = TimeSpan.FromSeconds(27.66);

    [DataField, AutoNetworkedField]
    public TimeSpan LandingDuration = TimeSpan.FromSeconds(18);

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(3, 3);

    [DataField]
    public List<Vector2i> FootprintOffsets = new();

    [DataField, AutoNetworkedField]
    public int AirborneMapOffset = 1;

    [DataField, AutoNetworkedField]
    public int GroundMapOffset = -1;

    [DataField, AutoNetworkedField]
    public EntityUid? Shadow;

    [DataField, AutoNetworkedField]
    public EntityUid? Downwash;

    [DataField, AutoNetworkedField]
    public Vector2 DownwashOffset = new(0f, 0.94f);

    [DataField, AutoNetworkedField]
    public TimeSpan TransitionEndTime;

    [DataField, AutoNetworkedField]
    public EntProtoId ShadowPrototype = "CMUBlackfootShadow";

    [DataField, AutoNetworkedField]
    public EntProtoId DownwashPrototype = "CMUBlackfootDownwash";
}

[RegisterComponent]
public sealed partial class BlackfootVisualsComponent : Component
{
    [DataField]
    public string BaseLayer = "base";

    [DataField]
    public string LightsLayer = "lights";

    [DataField]
    public string ThrustLayer = "thrust";

    [DataField]
    public string FansLayer = "fans";

    [DataField]
    public string DownwashLayer = "downwash";

    [DataField]
    public string DamageLayer = "damage";

    [DataField]
    public string ThrustersLayer = "thrusters";

    [DataField]
    public string LaunchersLayer = "launchers";

    [DataField]
    public string DoorGunLayer = "door-gun";

    [DataField]
    public string SupportLayer = "support";

    [DataField]
    public string SensorsLayer = "sensors";

    [DataField]
    public float DamageVisibleIntegrityFraction = 0.5f;

    [DataField]
    public float TakeoffLiftStartFraction = 0.5f;

    [DataField]
    public float TakeoffLiftOffset = 0.75f;
}

[RegisterComponent]
public sealed partial class BlackfootSoundComponent : Component
{
    [DataField]
    public SoundSpecifier? EngineIdleLoopSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/engineidle.wav");

    [DataField]
    public SoundSpecifier? ExteriorFlightLoopSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/exteriorflight.wav");

    [DataField]
    public SoundSpecifier? InteriorFlightLoopSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/interior.wav");

    [DataField]
    public SoundSpecifier? EngineStartupSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/enginestartup.wav");

    [DataField]
    public SoundSpecifier? EngineShutdownSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/engineshutdown.wav");

    [DataField]
    public SoundSpecifier? TakeoffSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/takeoff.wav");

    [DataField]
    public SoundSpecifier? LandingSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/landing.wav");

    [DataField]
    public SoundSpecifier? FlightTransitionSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/flight_transition.wav");

    [DataField]
    public SoundSpecifier? MechanicalSound = new SoundPathSpecifier("/Audio/_CMU14/Blackfoot/mechanical.wav");

    public EntityUid? InteriorEngineLoopStream;

    public string? InteriorEngineLoopSoundKey;

    public readonly HashSet<EntityUid> InteriorEngineLoopRecipients = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootFuelPowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Fuel = 600f;

    [DataField, AutoNetworkedField]
    public float MaxFuel = 600f;

    [DataField, AutoNetworkedField]
    public float Battery = 600f;

    [DataField, AutoNetworkedField]
    public float MaxBattery = 600f;

    [DataField, AutoNetworkedField]
    public float IdleFuelDrain = 0.5f;

    [DataField("vtolFuelDrain"), AutoNetworkedField]
    public float VTOLFuelDrain = 3f;

    [DataField, AutoNetworkedField]
    public float FlightFuelDrain = 1f;

    [DataField, AutoNetworkedField]
    public float SensorBatteryDrain = 1f;

    [DataField, AutoNetworkedField]
    public float MinimumTakeoffFuel = 30f;

    [DataField, AutoNetworkedField]
    public bool CrashOnZeroFuel = true;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootPilotActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public EntProtoId EngineToggleActionId = "ActionBlackfootEngineToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? EngineToggleAction;

    [DataField, AutoNetworkedField]
    public EntProtoId TakeoffActionId = "ActionBlackfootTakeoff";

    [DataField, AutoNetworkedField]
    public EntityUid? TakeoffAction;

    [DataField, AutoNetworkedField]
    public EntProtoId LandActionId = "ActionBlackfootLand";

    [DataField, AutoNetworkedField]
    public EntityUid? LandAction;

    [DataField, AutoNetworkedField]
    public EntProtoId FlightModeToggleActionId = "ActionBlackfootFlightModeToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? FlightModeToggleAction;

    [DataField, AutoNetworkedField]
    public EntProtoId RearDoorToggleActionId = "ActionBlackfootRearDoorToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? RearDoorToggleAction;

    [DataField, AutoNetworkedField]
    public EntProtoId StowToggleActionId = "ActionBlackfootStowToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? StowToggleAction;

    [DataField, AutoNetworkedField]
    public EntProtoId AscendZLevelActionId = "ActionBlackfootAscendZLevel";

    [DataField, AutoNetworkedField]
    public EntityUid? AscendZLevelAction;

    [DataField, AutoNetworkedField]
    public EntProtoId DescendZLevelActionId = "ActionBlackfootDescendZLevel";

    [DataField, AutoNetworkedField]
    public EntityUid? DescendZLevelAction;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootDoorGunActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public EntProtoId ZModeToggleActionId = "ActionBlackfootDoorGunZModeToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? ZModeToggleAction;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BlackfootDoorGunSeatComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootShadowComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Aircraft;

    [DataField, AutoNetworkedField]
    public int ProjectedMapOffset = -1;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootDownwashComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Aircraft;

    [DataField, AutoNetworkedField]
    public int ProjectedMapOffset = -1;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootRearDoorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField, AutoNetworkedField]
    public int RearEntryIndex;

    [DataField, AutoNetworkedField]
    public bool AllowAirborneExit;

    [DataField, AutoNetworkedField]
    public Vector2? InteriorExit;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BlackfootRearDoorControlComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootRearDoorVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField, AutoNetworkedField]
    public string DoorLayer = "door";

    [DataField, AutoNetworkedField]
    public string OverlayLayer = string.Empty;

    [DataField, AutoNetworkedField]
    public string OpenState = "rear door open";

    [DataField, AutoNetworkedField]
    public string ClosedState = "rear door closed";

    [DataField, AutoNetworkedField]
    public bool ShowOverlay;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootTowComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool CanTow;

    [DataField, AutoNetworkedField]
    public bool CanBeTowed = true;

    [DataField, AutoNetworkedField]
    public bool AllowAirborneTowing;

    [DataField, AutoNetworkedField]
    public bool AllowStowedTowing = true;

    [DataField, AutoNetworkedField]
    public bool AllowCrashedTowing = true;

    [DataField, AutoNetworkedField]
    public EntityUid? TowVehicle;

    [DataField, AutoNetworkedField]
    public EntityUid? TowedEntity;

    [DataField, AutoNetworkedField]
    public string? TowHardpointId;

    [DataField, AutoNetworkedField]
    public float AttachRange = 0.85f;

    [DataField, AutoNetworkedField]
    public Vector2 AttachOffset = new(0f, -1f);

    [DataField, AutoNetworkedField]
    public float AttachRotationDegrees = 180f;

    [DataField, AutoNetworkedField]
    public float TaxiSpeedMultiplier = 0.45f;

    [DataField, AutoNetworkedField]
    public float TaxiAccelerationMultiplier = 0.55f;

    public bool RestorePullableOnDetach;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootLandingPadComponent : Component
{
    [DataField, AutoNetworkedField]
    public BlackfootLandingPadState State = BlackfootLandingPadState.Folded;

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(3, 3);

    [DataField, AutoNetworkedField]
    public EntityUid? ParkedAircraft;

    [DataField, AutoNetworkedField]
    public bool Refueling;

    [DataField, AutoNetworkedField]
    public bool Recharging;

    [DataField, AutoNetworkedField]
    public float FuelRate = 5f;

    [DataField, AutoNetworkedField]
    public float BatteryRate = 5f;

    [DataField, AutoNetworkedField]
    public EntityUid? FuelPump;

    [DataField, AutoNetworkedField]
    public float FuelPumpSearchRange = 8f;

    [DataField, AutoNetworkedField]
    public bool RequireFuelPump = true;

    [DataField, AutoNetworkedField]
    public Vector2 FuelPumpOffset = new(-1.40625f, 0f);

    [DataField, AutoNetworkedField]
    public Vector2 FlightComputerOffset = new(-1.5f, -1.5f);

    [DataField]
    public EntProtoId? LightPrototype = "CMUBlackfootLandingPadLight";

    [DataField]
    public List<Vector2> LightOffsets = new()
    {
        new(-1.5f, -1.5f),
        new(-1.5f, 1.5f),
        new(1.5f, -1.5f),
        new(1.5f, 1.5f),
    };

    [DataField]
    public List<EntityUid> Lights = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootFlightComputerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LandingPad;

    [DataField, AutoNetworkedField]
    public float PadSearchRange = 8f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootFuelPumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LandingPad;

    [DataField, AutoNetworkedField]
    public float PadSearchRange = 8f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootLandingPadLightComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LandingPad;

    [DataField, AutoNetworkedField]
    public float PadSearchRange = 8f;

    [DataField, AutoNetworkedField]
    public BlackfootLandingPadLightState State = BlackfootLandingPadLightState.Off;
}

[RegisterComponent]
public sealed partial class BlackfootDeployableSupportComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public bool DeleteOnDeploy = true;

    [DataField]
    public Vector2 Offset;

    [DataField]
    public string DeployPopup = "The Blackfoot support assembly is deployed.";

    [DataField]
    public ProtoId<ToolQualityPrototype> DeployTool = "Anchoring";

    [DataField]
    public float DeployDelay = 2f;

    [DataField]
    public string ToolPopup = "Use a wrench to assemble the Blackfoot support equipment.";

    [DataField]
    public bool RequireClearFootprint;

    [DataField]
    public Vector2i ClearFootprint = new(3, 3);

    [DataField]
    public bool RequireLandingPad;

    [DataField]
    public float LandingPadSearchRange = 2.75f;

    [DataField]
    public Vector2 LandingPadOffset;

    [DataField]
    public BlackfootLandingPadAttachment LandingPadAttachment;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootPackableSupportComponent : Component
{
    [DataField(required: true)]
    public EntProtoId PackedPrototype;

    [DataField, AutoNetworkedField]
    public BlackfootSupportPackStage Stage = BlackfootSupportPackStage.Secured;

    [DataField]
    public ProtoId<ToolQualityPrototype> InitialTool = "Anchoring";

    [DataField]
    public ProtoId<ToolQualityPrototype> PanelTool = "Screwing";

    [DataField]
    public ProtoId<ToolQualityPrototype> FinalTool = "Anchoring";

    [DataField]
    public float InitialDelay = 2f;

    [DataField]
    public float PanelDelay = 2f;

    [DataField]
    public float FinalDelay = 2f;

    [DataField]
    public string ToolPopup = "Use a wrench, then a screwdriver, then a wrench again to pack this Blackfoot support equipment.";

    [DataField]
    public string InitialPopup = "The anchor bolts are loosened.";

    [DataField]
    public string PanelPopup = "The service panel is opened.";

    [DataField]
    public string PackedPopup = "The Blackfoot support equipment is packed up.";
}

[Serializable, NetSerializable]
public sealed partial class BlackfootSupportDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BlackfootSupportInitialWrenchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BlackfootSupportPanelScrewdriverDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BlackfootSupportFinalWrenchDoAfterEvent : SimpleDoAfterEvent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootStealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public bool HideTacticalMapMarker = true;

    [DataField, AutoNetworkedField]
    public bool DisableWeapons = true;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackfootSensorArrayComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public bool NightVisionEnabled;

    [DataField, AutoNetworkedField]
    public float Range = 45f;

    [DataField, AutoNetworkedField]
    public float BatteryDrain = 1f;
}
