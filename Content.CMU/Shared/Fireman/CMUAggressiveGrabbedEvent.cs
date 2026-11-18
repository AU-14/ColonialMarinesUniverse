namespace Content.Shared.CMU14.Fireman;

/// <summary>
/// Raised on the carrier (and broadcast) after FiremanCarrySystem.TryAggressiveGrab upgrades a pull to an aggressive
/// grab, whether from the second Ctrl-click or a one-click Machine Grip (ARCH-04). Also raised during prediction.
/// </summary>
[ByRefEvent]
public readonly record struct CMUAggressiveGrabbedEvent(EntityUid Carrier, EntityUid Target);
