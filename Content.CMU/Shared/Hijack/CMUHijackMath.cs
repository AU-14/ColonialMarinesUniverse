namespace Content.Shared.CMU14.Hijack;

/// <summary>Units are percent per two-second tick and seconds, as in SShijack.</summary>
public static class CMUHijackMath
{
    public const double TickSeconds = 2;
    public const double EarlyLaunchProgress = 25;
    public const double FtlProgress = 50;
    public const double CompleteProgress = 100;

    public static double SelfDestructDuration(int generators, int maximum = 18)
    {
        var fraction = Math.Round(Math.Clamp(generators, 0, maximum) / (double) maximum,
            2, MidpointRounding.AwayFromZero);
        return 300 + (1 - fraction) * 600;
    }

    public static double RescaleSelfDestruct(double remaining, int oldCount, int newCount, int maximum = 18)
        => Math.Max(0, remaining) / SelfDestructDuration(oldCount, maximum) * SelfDestructDuration(newCount, maximum);

    // CM-SS13 uses this older threshold formula for the room heat/halfway warnings,
    // independently of the linear 15-to-5-minute overload rescaling above.
    public static double SelfDestructWarningDuration(int generators, int maximum = 18)
        => Math.Max((1 - Math.Round(Math.Clamp(generators, 0, maximum) / (double) maximum,
            2, MidpointRounding.AwayFromZero)) * 900, 300);
}
