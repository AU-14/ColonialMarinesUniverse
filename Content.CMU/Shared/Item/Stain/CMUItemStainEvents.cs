namespace Content.Shared._CMU14.Item.Stain;

/// <summary>
/// Raised on a cleaning target so CMU systems can make it eligible for the forensic cleaning do-after.
/// </summary>
[ByRefEvent]
public record struct CMUCleaningEligibilityEvent
{
    public bool CanClean;
    public float DistanceThreshold;

    public CMUCleaningEligibilityEvent(bool canClean, float distanceThreshold)
    {
        CanClean = canClean;
        DistanceThreshold = distanceThreshold;
    }
}
