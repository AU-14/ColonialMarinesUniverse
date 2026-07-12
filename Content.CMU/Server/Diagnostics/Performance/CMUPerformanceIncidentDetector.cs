namespace Content.Server._CMU14.Diagnostics.Performance;

[Flags]
internal enum CMUPerformanceIncidentReason
{
    None = 0,
    FrameStall = 1 << 0,
    LowTps = 1 << 1,
    LowFps = 1 << 2,
    EntityGrowth = 1 << 3,
    EntityChurn = 1 << 4,
    ComponentGrowth = 1 << 5,
    ComponentChurn = 1 << 6,
    SendBandwidth = 1 << 7,
    ReceiveBandwidth = 1 << 8,
    Allocation = 1 << 9,
}

internal enum CMUPerformanceIncidentTransition
{
    None,
    Started,
    Updated,
    Recovered,
}

internal readonly record struct CMUPerformanceIncidentThresholds(
    double StallMilliseconds,
    double LowTpsRatio,
    double LowFpsRatio,
    double BreachSeconds,
    double RecoveryRatio,
    double RecoverySeconds,
    double EntityGrowthPerMinute,
    double EntityChurnPerMinute,
    double ComponentGrowthPerMinute,
    double ComponentChurnPerMinute,
    double SendBytesPerSecond,
    double ReceiveBytesPerSecond,
    double AllocationBytesPerFrame);

internal readonly record struct CMUPerformanceIncidentSample(
    double ElapsedSeconds,
    double FrameMilliseconds,
    double AchievedTps,
    bool TpsValid,
    double AverageFps,
    double TargetTps,
    double EntityGrowthPerMinute,
    double EntityChurnPerMinute,
    double ComponentGrowthPerMinute,
    double ComponentChurnPerMinute,
    bool ChurnValid,
    double SendBytesPerSecond,
    double ReceiveBytesPerSecond,
    long AllocatedBytes,
    bool SuppressRateTriggers);

internal readonly record struct CMUPerformanceIncidentEvaluation(
    CMUPerformanceIncidentTransition Transition,
    CMUPerformanceIncidentReason Reasons,
    CMUPerformanceIncidentReason PreviousReasons);

/// <summary>
///     Pure incident state machine. Threshold breaches are coalesced into one incident and recovery is hysteretic.
/// </summary>
internal sealed class CMUPerformanceIncidentDetector
{
    private double _lowTpsBreachSeconds;
    private double _lowFpsBreachSeconds;
    private double _healthySeconds;

    public bool Active { get; private set; }
    public CMUPerformanceIncidentReason ActiveReasons { get; private set; }

    public CMUPerformanceIncidentEvaluation Evaluate(
        in CMUPerformanceIncidentSample sample,
        in CMUPerformanceIncidentThresholds thresholds)
    {
        double elapsed = Math.Max(0, sample.ElapsedSeconds);
        CMUPerformanceIncidentReason reasons = GetImmediateReasons(sample, thresholds);

        if (sample.SuppressRateTriggers)
        {
            _lowTpsBreachSeconds = 0;
            _lowFpsBreachSeconds = 0;
        }
        else
        {
            reasons |= EvaluateLowTps(sample, thresholds, elapsed);
            reasons |= EvaluateLowFps(sample, thresholds, elapsed);

            if (sample.ChurnValid)
                reasons |= GetChurnReasons(sample, thresholds);
        }

        if (!Active)
        {
            if (reasons == CMUPerformanceIncidentReason.None)
                return new(CMUPerformanceIncidentTransition.None, reasons, CMUPerformanceIncidentReason.None);

            Active = true;
            ActiveReasons = reasons;
            _healthySeconds = 0;
            return new(CMUPerformanceIncidentTransition.Started, reasons, CMUPerformanceIncidentReason.None);
        }

        CMUPerformanceIncidentReason previous = ActiveReasons;
        if (reasons != CMUPerformanceIncidentReason.None)
        {
            _healthySeconds = 0;
            ActiveReasons = reasons;
            CMUPerformanceIncidentTransition transition = reasons == previous
                ? CMUPerformanceIncidentTransition.None
                : CMUPerformanceIncidentTransition.Updated;
            return new(transition, reasons, previous);
        }

        _healthySeconds += elapsed;
        if (_healthySeconds < Math.Max(0, thresholds.RecoverySeconds))
            return new(CMUPerformanceIncidentTransition.None, previous, previous);

        ResetIncidentState();
        return new(CMUPerformanceIncidentTransition.Recovered, CMUPerformanceIncidentReason.None, previous);
    }

    public void Reset()
    {
        ResetIncidentState();
        _lowTpsBreachSeconds = 0;
        _lowFpsBreachSeconds = 0;
    }

    public static string FormatReasons(CMUPerformanceIncidentReason reasons)
    {
        if (reasons == CMUPerformanceIncidentReason.None)
            return "none";

        var values = new List<string>();
        Add(CMUPerformanceIncidentReason.FrameStall, "frame-stall");
        Add(CMUPerformanceIncidentReason.LowTps, "low-tps");
        Add(CMUPerformanceIncidentReason.LowFps, "low-fps");
        Add(CMUPerformanceIncidentReason.EntityGrowth, "entity-growth");
        Add(CMUPerformanceIncidentReason.EntityChurn, "entity-churn");
        Add(CMUPerformanceIncidentReason.ComponentGrowth, "component-growth");
        Add(CMUPerformanceIncidentReason.ComponentChurn, "component-churn");
        Add(CMUPerformanceIncidentReason.SendBandwidth, "send-bandwidth");
        Add(CMUPerformanceIncidentReason.ReceiveBandwidth, "receive-bandwidth");
        Add(CMUPerformanceIncidentReason.Allocation, "allocation");
        return string.Join(',', values);

        void Add(CMUPerformanceIncidentReason reason, string name)
        {
            if ((reasons & reason) != 0)
                values.Add(name);
        }
    }

    private CMUPerformanceIncidentReason EvaluateLowTps(
        in CMUPerformanceIncidentSample sample,
        in CMUPerformanceIncidentThresholds thresholds,
        double elapsed)
    {
        if (!sample.TpsValid || thresholds.LowTpsRatio <= 0 || sample.TargetTps <= 0)
        {
            _lowTpsBreachSeconds = 0;
            return CMUPerformanceIncidentReason.None;
        }

        double ratio = sample.AchievedTps / sample.TargetTps;
        if (ActiveReasons.HasFlag(CMUPerformanceIncidentReason.LowTps) && ratio < thresholds.RecoveryRatio)
            return CMUPerformanceIncidentReason.LowTps;

        if (ratio >= thresholds.LowTpsRatio)
        {
            _lowTpsBreachSeconds = 0;
            return CMUPerformanceIncidentReason.None;
        }

        _lowTpsBreachSeconds += elapsed;
        return _lowTpsBreachSeconds >= Math.Max(0, thresholds.BreachSeconds)
            ? CMUPerformanceIncidentReason.LowTps
            : CMUPerformanceIncidentReason.None;
    }

    private CMUPerformanceIncidentReason EvaluateLowFps(
        in CMUPerformanceIncidentSample sample,
        in CMUPerformanceIncidentThresholds thresholds,
        double elapsed)
    {
        if (thresholds.LowFpsRatio <= 0 || sample.TargetTps <= 0)
        {
            _lowFpsBreachSeconds = 0;
            return CMUPerformanceIncidentReason.None;
        }

        double ratio = sample.AverageFps / sample.TargetTps;
        if (ActiveReasons.HasFlag(CMUPerformanceIncidentReason.LowFps) && ratio < thresholds.RecoveryRatio)
            return CMUPerformanceIncidentReason.LowFps;

        if (ratio >= thresholds.LowFpsRatio)
        {
            _lowFpsBreachSeconds = 0;
            return CMUPerformanceIncidentReason.None;
        }

        _lowFpsBreachSeconds += elapsed;
        return _lowFpsBreachSeconds >= Math.Max(0, thresholds.BreachSeconds)
            ? CMUPerformanceIncidentReason.LowFps
            : CMUPerformanceIncidentReason.None;
    }

    private static CMUPerformanceIncidentReason GetImmediateReasons(
        in CMUPerformanceIncidentSample sample,
        in CMUPerformanceIncidentThresholds thresholds)
    {
        CMUPerformanceIncidentReason reasons = CMUPerformanceIncidentReason.None;

        if (thresholds.StallMilliseconds > 0 && sample.FrameMilliseconds >= thresholds.StallMilliseconds)
            reasons |= CMUPerformanceIncidentReason.FrameStall;
        if (thresholds.SendBytesPerSecond > 0 && sample.SendBytesPerSecond >= thresholds.SendBytesPerSecond)
            reasons |= CMUPerformanceIncidentReason.SendBandwidth;
        if (thresholds.ReceiveBytesPerSecond > 0 && sample.ReceiveBytesPerSecond >= thresholds.ReceiveBytesPerSecond)
            reasons |= CMUPerformanceIncidentReason.ReceiveBandwidth;
        if (thresholds.AllocationBytesPerFrame > 0 && sample.AllocatedBytes >= thresholds.AllocationBytesPerFrame)
            reasons |= CMUPerformanceIncidentReason.Allocation;

        return reasons;
    }

    private static CMUPerformanceIncidentReason GetChurnReasons(
        in CMUPerformanceIncidentSample sample,
        in CMUPerformanceIncidentThresholds thresholds)
    {
        CMUPerformanceIncidentReason reasons = CMUPerformanceIncidentReason.None;

        if (thresholds.EntityGrowthPerMinute > 0 &&
            sample.EntityGrowthPerMinute >= thresholds.EntityGrowthPerMinute)
            reasons |= CMUPerformanceIncidentReason.EntityGrowth;
        if (thresholds.EntityChurnPerMinute > 0 &&
            sample.EntityChurnPerMinute >= thresholds.EntityChurnPerMinute)
            reasons |= CMUPerformanceIncidentReason.EntityChurn;
        if (thresholds.ComponentGrowthPerMinute > 0 &&
            sample.ComponentGrowthPerMinute >= thresholds.ComponentGrowthPerMinute)
            reasons |= CMUPerformanceIncidentReason.ComponentGrowth;
        if (thresholds.ComponentChurnPerMinute > 0 &&
            sample.ComponentChurnPerMinute >= thresholds.ComponentChurnPerMinute)
            reasons |= CMUPerformanceIncidentReason.ComponentChurn;

        return reasons;
    }

    private void ResetIncidentState()
    {
        Active = false;
        ActiveReasons = CMUPerformanceIncidentReason.None;
        _healthySeconds = 0;
    }
}
