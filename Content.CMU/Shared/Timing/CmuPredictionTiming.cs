using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Timing;

/// <summary>
/// Gameplay time shared by a render-frame request and its simulation replay.
/// </summary>
public static class CmuPredictionTiming
{
    /// <summary>
    /// Returns the current tick's gameplay time without changing simulation or prediction flags.
    /// </summary>
    public static TimeSpan GetSimulationTime(IGameTiming timing)
    {
        return timing.CurTime - (timing.InSimulation ? TimeSpan.Zero : timing.TickRemainder);
    }
}
