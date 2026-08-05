namespace Content.Shared._RMC14.TacticalMap;

public static class AreaInfoUpdateThrottle
{
    public static bool ShouldUpdate(TimeSpan time, TimeSpan lastUpdate, TimeSpan interval)
    {
        return time >= lastUpdate + interval;
    }
}
