using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.DroneOperator;

/// <summary>Hazards of the currently installed fuel; permanent corrosion and wreck state survive tank changes.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUFlamerDroneFuelComponent : Component
{
    [DataField, AutoNetworkedField] public float Sticky;
    [DataField, AutoNetworkedField] public float Hot;
    [DataField, AutoNetworkedField] public float Electrical;
    [DataField, AutoNetworkedField] public float Volatile;
    [DataField, AutoNetworkedField] public float Corrosive;
    [DataField, AutoNetworkedField] public Color Glow = Color.FromHex("#ff8e32");
    [DataField, AutoNetworkedField] public bool Ruined;
    [DataField, AutoNetworkedField] public float RepairEfficiency = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CoolingUntil;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ControlLockedUntil;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFault;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextWear;

    public float SpecialFraction => Sticky + Hot + Electrical + Volatile + Corrosive;

    public (float Sticky, float Hot, float Volatile)? FiredFuel;
}

[ByRefEvent]
public readonly record struct CMUCombatDroneWreckedEvent;

[ByRefEvent]
public readonly record struct CMUFlamerFuelChangedEvent;
