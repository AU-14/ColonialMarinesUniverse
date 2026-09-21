using Robust.Shared.GameStates;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Content.Shared.Doors.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Dropship.MultiDeck;

[RegisterComponent]
public sealed partial class MohawkMechanismsComponent : Component
{
    public bool RampDeployed;
    public bool HatchDeployed;
    public readonly Dictionary<EntityUid, Vector2> CabinRampMarkers = new();
    public readonly Dictionary<EntityUid, Vector2> CabinRampEdging = new();

    /// <summary>The lower boarding equipment was lost during a hijack crash.</summary>
    [DataField]
    public bool BoardingDisabled;

    /// <summary>Deployed ramp displacement from its raised cabin floor, in ship-local axes.</summary>
    [DataField]
    public Vector2 LoweredRampOffset = new(0, -1);

    [DataField]
    public HashSet<MohawkControlGroup> BrokenControls = new();
}

[Serializable, NetSerializable]
public sealed partial class MohawkSabotageDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MohawkRepairDoAfterEvent : SimpleDoAfterEvent;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MohawkRampMovingComponent : Component
{
    [DataField]
    public bool Deploying;

    [DataField]
    public int Step;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStep;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MohawkHatchMovingComponent : Component
{
    [DataField]
    public bool Deploying;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan FinishAt;
}

[RegisterComponent]
public sealed partial class MohawkControlComponent : Component
{
    [DataField]
    public MohawkControlGroup Group;
}

[RegisterComponent]
public sealed partial class MohawkRampSegmentComponent : Component
{
    [DataField]
    public int Stage;

    [DataField]
    public bool Lower;

    /// <summary>Ordinary multi-Z stair profile, present only on the upper ramp row.</summary>
    [DataField]
    public List<float>? HeightCurve;

    public Tile? ClosedTile;
    public bool Deployed;
}

[RegisterComponent]
public sealed partial class MohawkHatchComponent : Component;

[RegisterComponent]
public sealed partial class MohawkRampEdgingComponent : Component;

[RegisterComponent]
public sealed partial class MohawkLowerLadderComponent : Component;

[RegisterComponent]
public sealed partial class MohawkLandingCrushComponent : Component;

[RegisterComponent]
public sealed partial class MohawkGunnerySeatComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class MohawkSeatComponent : Component
{
    /// <summary>Passenger artwork displacement in seat-local axes, without moving their collision body.</summary>
    [DataField]
    public Vector2 VisualOffset;
}

[RegisterComponent]
public sealed partial class MohawkLandingGearComponent : Component;

[Serializable, NetSerializable]
public enum MohawkControlGroup : byte
{
    Ramp,
    Hatch,
    Port,
    Starboard,
}

[Serializable, NetSerializable]
public enum MohawkVisuals : byte
{
    Layer,
    Deployed,
    HatchState,
}

/// <summary>Allows non-door boarding mechanisms to follow the flight console's door controls.</summary>
[ByRefEvent]
public readonly record struct DropshipDoorControlEvent(DoorLocation Location, bool? Locked);

[ByRefEvent]
public readonly record struct DropshipBoardingChangedEvent;

/// <summary>A hijack flight has been accepted and is about to enter its launch sequence.</summary>
[ByRefEvent]
public readonly record struct DropshipHijackFlightEvent;
