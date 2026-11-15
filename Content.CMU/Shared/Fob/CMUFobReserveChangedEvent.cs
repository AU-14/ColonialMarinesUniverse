using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

[Serializable, NetSerializable]
public enum CMUFobReserveState : byte
{
    Standby,
    Starting,
    Active,
    Empty,
    Closed,
}

/// <summary>
/// Server-local notification on the network entity. Only Active permits powered support output.
/// No gun, ordinary defense, or evacuation system is implicitly controlled by this event.
/// </summary>
[ByRefEvent]
public readonly record struct CMUFobReserveChangedEvent(CMUFobReserveState State, TimeSpan Remaining);
