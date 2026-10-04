namespace Content.Shared._CMU14.ZLevels.Vehicles;

public readonly record struct CMUVehicleSupportState(
    int SupportedSamples,
    int TotalSamples,
    float UnsupportedFraction,
    float AverageSupportedDistance,
    bool StickyGround)
{
    public bool ShouldTip(float edgeTipUnsupportedFraction)
    {
        return CMUVehicleSupportFootprint.ShouldTip(
            SupportedSamples,
            TotalSamples,
            edgeTipUnsupportedFraction);
    }
}
