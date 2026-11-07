using Content.Shared._RMC14.NightVision;
using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle;

[RegisterComponent]
public sealed partial class CMUVehicleVisionOperatorComponent : Component
{
    public EntityUid Vehicle;
    public EntityUid? NightVisionAction;
    public EntityUid? ThermalVisionAction;
    public CMUVehicleVisionMode Mode;
    public CMUVehicleVisionMode ModeBeforeThermal;
    public TimeSpan ThermalReadyAt;
    public TimeSpan ThermalUntil;
    public bool ThermalActionWasReady = true;
    public bool CapturedNightVision;
    public NightVisionSettings? PreviousNightVision;
}

public enum CMUVehicleVisionMode
{
    Off,
    NightVision,
    Thermal,
}

public sealed partial class CMUVehicleNightVisionActionEvent : InstantActionEvent;

public sealed partial class CMUVehicleThermalVisionActionEvent : InstantActionEvent;
