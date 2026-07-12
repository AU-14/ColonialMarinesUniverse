namespace Content.Client.CombatMode;

internal enum ZLevelCrosshairIndicator
{
    None,
    Up,
    Down,
}

internal static class ZLevelCrosshairIndicatorHelper
{
    public static ZLevelCrosshairIndicator Get(bool shootUp, bool shootDown)
    {
        if (shootDown)
            return ZLevelCrosshairIndicator.Down;

        return shootUp
            ? ZLevelCrosshairIndicator.Up
            : ZLevelCrosshairIndicator.None;
    }

    public static string? GetGlyph(ZLevelCrosshairIndicator indicator)
    {
        return indicator switch
        {
            ZLevelCrosshairIndicator.Up => "^",
            ZLevelCrosshairIndicator.Down => "v",
            _ => null,
        };
    }
}
