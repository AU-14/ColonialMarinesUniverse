using Robust.Shared.Map;

namespace Content.Shared.CMU14.Dropship;

/// <summary>
/// Raised on the server when a fulton is deployed on a body, so it can be called in over the radio.
/// </summary>
[ByRefEvent]
public readonly record struct CMUFultonDeployedEvent(
    EntityUid User,
    EntityUid Target,
    EntityCoordinates Location,
    TimeSpan AvailableFor);

/// <summary>
/// Raised on the server when a medevac stretcher's beacon comes on, or a patient is strapped onto a stretcher whose
/// beacon is already on.
/// </summary>
[ByRefEvent]
public readonly record struct CMUMedevacBeaconEvent(EntityUid Stretcher, EntityUid? User, bool PatientStrapped);
