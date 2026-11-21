using Content.Server.CMU14.ColonyEconomy;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Shared payout rules for the corporate minigame terminals. Every terminal pays into the
///     corporate budget through here, so repeat reductions work the same everywhere.
/// </summary>
public sealed class CMUCorporateWorkSystem : EntitySystem
{
    [Dependency] private CorporateConsoleSystem _corporate = default!;

    /// <summary>
    ///     Each earlier completion of the same task by this worker multiplies the payout by this.
    /// </summary>
    public const float RepeatFactor = 0.85f;

    /// <summary>
    ///     Repeats never drop a payout below this fraction of the full reward.
    /// </summary>
    public const float MinimumFactor = 0.25f;

    /// <summary>
    ///     What <paramref name="worker"/> would earn for <paramref name="task"/> right now.
    /// </summary>
    public int GetPayout(EntityUid worker, string task, float reward)
    {
        var completed = 0;
        if (TryComp<CMUCorporateWorkerComponent>(worker, out var record))
            completed = record.Completed.GetValueOrDefault(task);

        var factor = MathF.Max(MinimumFactor, MathF.Pow(RepeatFactor, completed));
        return (int) MathF.Round(reward * factor);
    }

    /// <summary>
    ///     Pays the corporate budget for a finished task and counts it against the worker.
    ///     Returns the amount paid; nothing is paid or counted for a reward of zero or less.
    /// </summary>
    public int Pay(EntityUid worker, string task, float reward)
    {
        if (reward <= 0)
            return 0;

        var payout = GetPayout(worker, task, reward);
        var record = EnsureComp<CMUCorporateWorkerComponent>(worker);
        record.Completed[task] = record.Completed.GetValueOrDefault(task) + 1;

        _corporate.AddToCorporateBudget(payout);
        return payout;
    }
}
