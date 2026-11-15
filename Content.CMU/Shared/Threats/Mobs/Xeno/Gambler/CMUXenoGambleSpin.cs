namespace Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;

public static class CMUXenoGambleSpin
{
    private static TimeSpan GetFastDelay(CMUXenoGamblerComponent gambler)
    {
        return gambler.SpinDelays.Count > 0 && gambler.SpinDelays[0] > TimeSpan.Zero
            ? gambler.SpinDelays[0]
            : gambler.ReelStopInterval;
    }

    private static int GetExtraSteps(CMUXenoGamblerComponent gambler, int reel)
    {
        var fast = GetFastDelay(gambler);
        return fast <= TimeSpan.Zero ? 0 : (int) Math.Round(reel * (gambler.ReelStopInterval / fast));
    }

    public static int GetReelSteps(CMUXenoGamblerComponent gambler, int reel)
    {
        return GetExtraSteps(gambler, reel) + gambler.SpinDelays.Count;
    }

    public static double GetReelPosition(CMUXenoGamblerComponent gambler, TimeSpan start, TimeSpan time, int reel)
    {
        var elapsed = time - start;
        if (elapsed <= TimeSpan.Zero)
            return 0;

        var fast = GetFastDelay(gambler);
        var extra = GetExtraSteps(gambler, reel);
        var fastTime = fast * extra;
        if (elapsed < fastTime)
            return elapsed / fast;

        elapsed -= fastTime;
        double position = extra;
        foreach (var delay in gambler.SpinDelays)
        {
            if (elapsed < delay)
                return position + elapsed / delay;

            elapsed -= delay;
            position++;
        }

        return position;
    }

    /// <summary>when it stops spinning</summary>
    public static TimeSpan GetStopTime(CMUXenoGamblerComponent gambler, TimeSpan start, int reel)
    {
        var time = start + GetFastDelay(gambler) * GetExtraSteps(gambler, reel);
        foreach (var delay in gambler.SpinDelays)
        {
            time += delay;
        }

        return time;
    }

    /// <summary>Time from start spin till last spin</summary>
    public static TimeSpan GetDuration(CMUXenoGamblerComponent gambler, int reels)
    {
        return reels <= 0 ? TimeSpan.Zero : GetStopTime(gambler, TimeSpan.Zero, reels - 1);
    }
}
