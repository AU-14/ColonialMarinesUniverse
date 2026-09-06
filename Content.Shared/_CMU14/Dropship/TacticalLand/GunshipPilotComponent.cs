using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Dropship.Integrity;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipPilotSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    [DataField, AutoNetworkedField]
    public EntityUid? Eye;

    public EntityUid? UpperCameraEye;

    public EntityUid? LowerCameraEye;

    [DataField, AutoNetworkedField]
    public int ViewOffset;

    [DataField, AutoNetworkedField]
    public bool RearView;

    /// <summary>
    /// Pilot-selected input power. Momentum is preserved when this is reduced;
    /// it only scales new translational and rotational thrust.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ThrustPercent = 100f;

    [DataField]
    public float TranslationAcceleration = 8f;

    [DataField]
    public float MaxTranslationSpeed = 32f;

    [DataField]
    public float RotationAccelerationDegrees = 60f;

    [DataField]
    public float MaxRotationSpeedDegrees = 180f;

    [DataField]
    public EntProtoId EyePrototype = "CMUGunshipPilotEye";

    [DataField]
    public EntProtoId MasterAlarmActionId = "ActionGunshipMasterAlarmSilence";

    [DataField]
    public EntProtoId CameraCycleActionId = "ActionGunshipCycleCamera";

    [DataField]
    public EntProtoId DropshipOutlineActionId = "ActionGunshipDropshipOutline";

    [DataField]
    public EntProtoId PilotPanningActionId = "ActionGunshipPilotPanning";

    [DataField]
    public EntProtoId PilotZoomActionId = "ActionGunshipPilotZoom";

    public GunshipControlInput HeldInputs;
    public ushort PressedActions;
    public EntityUid? MasterAlarmAction;
    public EntityUid? CameraCycleAction;
    public EntityUid? DropshipOutlineAction;
    public EntityUid? PilotPanningAction;
    public EntityUid? PilotZoomAction;
    public GunshipManeuveringCamera ManeuveringCamera;
    public bool ShowDropshipOutline = true;
    public bool PilotPanning = true;
    public bool PilotZoom;
    public Vector2 OriginalZoom = Vector2.One;
    public float OriginalPvsScale = 1f;
    public bool AddedCursorOffset;
    public TimeSpan NextBlockedPopup;
    public TimeSpan NextCameraUpdate;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipPilotEyeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(9, 17);

    [DataField, AutoNetworkedField]
    public float RotationDegrees;

    [DataField, AutoNetworkedField]
    public int ViewOffset;

    [DataField, AutoNetworkedField]
    public bool RearView;

}

/// <summary>
/// Added to a helmet while an MK30 flight visor is lowered.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipPilotVisorComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color NightVisionTint = new(0.65f, 0.72f, 0.78f);

    [DataField, AutoNetworkedField]
    public float NightVisionNoiseStrength = 0.02f;

    [DataField, AutoNetworkedField]
    public float NightVisionVignetteStrength;
}

/// <summary>
/// Replicated pilot display data. Its presence means the flight visor is lowered.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunshipPilotHudComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Visor;

    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public bool FlightControlsAvailable;

    [DataField, AutoNetworkedField]
    public Vector2 LinearVelocity;

    [DataField, AutoNetworkedField]
    public float ShipRotationDegrees;

    [DataField, AutoNetworkedField]
    public float Integrity;

    [DataField, AutoNetworkedField]
    public float MaxIntegrity;

    [DataField, AutoNetworkedField]
    public float ThrustPercent = 100f;

    [DataField, AutoNetworkedField]
    public bool HasDirectFireWeapon;

    [DataField, AutoNetworkedField]
    public int DirectFireAmmo = -1;

    [DataField, AutoNetworkedField]
    public List<DropshipMalfunction> Malfunctions = new();

    [DataField, AutoNetworkedField]
    public List<DropshipAlarm> Alarms = new();

    [DataField, AutoNetworkedField]
    public bool MasterAlarmSilenced;

    [DataField, AutoNetworkedField]
    public int ViewOffset;

    [DataField, AutoNetworkedField]
    public bool RearView;

    [DataField, AutoNetworkedField]
    public GunshipManeuveringCamera ManeuveringCamera;

    [DataField, AutoNetworkedField]
    public bool ShowDropshipOutline = true;

    [DataField, AutoNetworkedField]
    public bool PilotPanning = true;

    [DataField, AutoNetworkedField]
    public bool PilotZoom;

    public bool AddedNightVisionItem;

    public bool AddedStaticZoomLevel;
}

public sealed partial class GunshipMasterAlarmToggleActionEvent : InstantActionEvent;

public sealed partial class GunshipCycleCameraActionEvent : InstantActionEvent;

public sealed partial class GunshipDropshipOutlineToggleActionEvent : InstantActionEvent;

public sealed partial class GunshipPilotPanningToggleActionEvent : InstantActionEvent;

public sealed partial class GunshipPilotZoomToggleActionEvent : InstantActionEvent;

/// <summary>
/// Raised on a tactically hovering dropship as soon as its crash countdown begins.
/// Pilot camera state must be removed before the grid changes maps at impact.
/// </summary>
[ByRefEvent]
public readonly record struct GunshipCrashStartedEvent;

[Flags]
public enum GunshipControlInput : ushort
{
    None = 0,
    Forward = 1 << 0,
    Back = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    RotateLeft = 1 << 4,
    RotateRight = 1 << 5,
}

public enum GunshipControlAction : byte
{
    Forward,
    Back,
    Left,
    Right,
    RotateLeft,
    RotateRight,
    Ascend,
    Descend,
    ViewUp,
    ViewDown,
    RearView,
}

public enum GunshipManeuveringCamera : byte
{
    None,
    Rear,
    Lower,
    Upper,
}

[Serializable, NetSerializable]
public sealed class GunshipControlInputEvent : EntityEventArgs
{
    public GunshipControlAction Action;
    public bool Pressed;

    public GunshipControlInputEvent()
    {
    }

    public GunshipControlInputEvent(GunshipControlAction action, bool pressed)
    {
        Action = action;
        Pressed = pressed;
    }
}

[Serializable, NetSerializable]
public sealed class GunshipThrustAdjustEvent : EntityEventArgs
{
    public int Steps;

    public GunshipThrustAdjustEvent()
    {
    }

    public GunshipThrustAdjustEvent(int steps)
    {
        Steps = steps;
    }
}

[Serializable, NetSerializable]
public sealed class GunshipCycleCameraInputEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GunshipPilotPanningInputEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GunshipOpenNavigationInputEvent : EntityEventArgs;
