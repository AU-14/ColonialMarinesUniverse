using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Fob;

/// <summary>Server-owned guidance captured from the firing gun, never from subsequent target selections.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUFobMissileComponent : Component
{
    [DataField] public float Speed = 12;
    [DataField] public float TrackingRange = 11;
    [DataField] public float ImpactRadius = 0.2f;
    [DataField] public TimeSpan FlightLifetime = TimeSpan.FromSeconds(12);
    [DataField] public FixedPoint2 T3CriticalMargin = 10;
    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public EntityUid Sensor;
    [ViewVariables] public EntityUid Network;
    [ViewVariables] public MapCoordinates LastPosition;
    [ViewVariables] public bool TrackingLost;
    [ViewVariables, AutoPausedField] public TimeSpan ExpiresAt;
}
